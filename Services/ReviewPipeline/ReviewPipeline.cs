using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Models.Entities;
using Models.ResponseModels.AIReview;
using Repository.Rules;
using Services.AIReview;
using Services.Rag;
using Services.Rule;
namespace Services.ReviewPipeline
{
    public class ReviewPipeline : IReviewPipeline
    {
        private static readonly string[] NoiseNames =
        {
        "package-lock.json", "yarn.lock", "pnpm-lock.yaml", "composer.lock", "cargo.lock", "go.sum", "packages.lock.json"
    };
        private static readonly string[] NoiseSuffixes =
        {
        ".min.js", ".min.css", ".map", ".svg", ".snap", ".designer.cs", ".g.cs", ".generated.cs", ".lock"
    };

        private readonly IAiReviewService _ai;
        private readonly IContextRetriever _retriever;
        private readonly IReviewRuleRepository _rules;
        private readonly ILogger<ReviewPipeline> _log;
        private readonly int _maxBatchChars, _maxBatches, _hardMaxFiles, _maxRuleChars;

        public ReviewPipeline(IAiReviewService ai, IContextRetriever retriever, IReviewRuleRepository rules,
            IConfiguration cfg, ILogger<ReviewPipeline> log)
        {
            _ai = ai; _retriever = retriever; _rules = rules; _log = log;
            _maxBatchChars = cfg.GetValue("Review:MaxBatchChars", 12000);
            _maxBatches = cfg.GetValue("Review:MaxBatches", 4);
            _hardMaxFiles = cfg.GetValue("Review:MaxFilesPerReview", 30);
            _maxRuleChars = cfg.GetValue("Review:MaxRuleChars", 4000);
        }

        public async Task<PipelineResult> RunAsync(Models.Entities.PullRequest pr, bool useRag, CancellationToken ct = default)
        {
            var repo = pr.Repository;
            var maxFiles = Math.Clamp(repo.MaxFilesPerReview > 0 ? repo.MaxFilesPerReview : 20, 1, _hardMaxFiles);

            var candidates = pr.PullRequestFiles
                .Where(f => !string.IsNullOrWhiteSpace(f.Patch) && !IsNoise(f.FileName))
                .ToList();

            var omitted = new List<string>();
            omitted.AddRange(candidates.Skip(maxFiles).Select(f => f.FileName));
            candidates = candidates.Take(maxFiles).ToList();

            var noRules = (IReadOnlyDictionary<int, string>)new Dictionary<int, string>();
            if (candidates.Count == 0)
            {
                return new PipelineResult(new AiReviewResult
                {
                    CodeQualityScore = 100,
                    SecurityScore = 100,
                    MaintainabilityScore = 100,
                    OverallScore = 100,
                    MergeRecommendation = true,
                    AiSummary = "No reviewable source changes (only binary, generated or lock files)."
                }, false, 0, null, omitted, noRules);
            }

            // ── organization rules (never fatal: a rules lookup problem must not stop the review) ──
            var rules = new List<ReviewRule>();
            if (repo.OrganizationId is int orgId)
            {
                try { rules = await _rules.GetActiveForRepositoryAsync(orgId, repo.Id); }
                catch (Exception ex) { _log.LogWarning(ex, "Loading review rules failed for repo {Id}; continuing without", repo.Id); }
            }

            // ── pack files into batches ──────────────────────────────────────────────
            var batches = new List<(List<PullRequestFile> Files, string Diff)>();
            var curFiles = new List<PullRequestFile>();
            var cur = new System.Text.StringBuilder();

            foreach (var f in candidates)
            {
                var block = BuildBlock(f);
                if (curFiles.Count > 0 && cur.Length + block.Length > _maxBatchChars)
                {
                    batches.Add((curFiles, cur.ToString()));
                    curFiles = new(); cur.Clear();
                }
                curFiles.Add(f);
                cur.Append(block);
            }
            if (curFiles.Count > 0) batches.Add((curFiles, cur.ToString()));

            if (batches.Count > _maxBatches)
            {
                omitted.AddRange(batches.Skip(_maxBatches).SelectMany(b => b.Files).Select(f => f.FileName));
                batches = batches.Take(_maxBatches).ToList();
            }

            // ── context + rules per batch ────────────────────────────────────────────
            var request = new ReviewRequest
            {
                PrTitle = pr.Title,
                PrDescription = pr.Description,
                RepoInstructions = repo.CustomInstructions
            };
            var sources = new List<string>();
            var chunks = 0;
            var usedRules = new Dictionary<int, ReviewRule>();

            foreach (var (files, diff) in batches)
            {
                string? ctx = null;
                if (useRag)
                {
                    try
                    {
                        var rc = await _retriever.BuildForFilesAsync(repo.Id, files, ct);
                        if (!rc.IsEmpty) { ctx = rc.Text; chunks += rc.ChunkCount; sources.AddRange(rc.Sources); }
                    }
                    catch (Exception ex)
                    {
                        _log.LogWarning(ex, "Context retrieval failed for PR #{N}; reviewing without context", pr.Number);
                    }
                }

                var paths = files.Select(f => f.FileName).ToList();
                var applicable = new List<RulePrompt>();
                var used = 0;
                foreach (var r in rules.Where(r => GlobMatcher.IsMatchAny(r.FilePattern, paths)))
                {
                    var len = r.Title.Length + r.Description.Length + 40;
                    if (used + len > _maxRuleChars) break;           // keep the prompt bounded (rules are sorted by severity)
                    used += len;
                    applicable.Add(new RulePrompt(r.Id, r.Title, r.Description, r.Severity, r.Category));
                    usedRules[r.Id] = r;
                }

                request.Batches.Add(new ReviewBatch { Diff = diff, Context = ctx, Rules = applicable });
            }

            if (omitted.Count > 0)
                request.SummaryNote = $"Note: {omitted.Count} file(s) were not reviewed because of size limits: " +
                                      string.Join(", ", omitted.Take(5)) + (omitted.Count > 5 ? ", ..." : ".");

            var result = await _ai.ReviewAsync(request, ct);
            ApplyRules(result, usedRules);

            var summary = sources.Count == 0 ? null : string.Join("; ", sources.Distinct().Take(25));
            if (summary is { Length: > 1900 }) summary = summary[..1900];

            var titles = usedRules.ToDictionary(kv => kv.Key, kv => kv.Value.Title);
            return new PipelineResult(result, chunks > 0, chunks, summary, omitted, titles);
        }

        /// <summary>
        /// The model only suggests which rule was violated. We make it binding: unknown rule ids are dropped, and an issue
        /// tied to a rule can never be LESS severe than the severity the organization set for that rule.
        /// </summary>
        private static void ApplyRules(AiReviewResult result, Dictionary<int, ReviewRule> rules)
        {
            foreach (var issue in result.Issues)
            {
                if (issue.RuleId is not int id) continue;
                if (!rules.TryGetValue(id, out var rule)) { issue.RuleId = null; continue; }

                if (Rank(rule.Severity) < Rank(issue.Severity)) issue.Severity = rule.Severity;
                if (string.IsNullOrWhiteSpace(issue.Category)) issue.Category = rule.Category ?? "Rule";
            }
        }

        private static int Rank(string s) => s switch { "HIGH" => 0, "MEDIUM" => 1, "LOW" => 2, _ => 3 };

        private string BuildBlock(PullRequestFile f)
        {
            var head = $"=== {f.FileName} ({f.Status}, +{f.Additions} -{f.Deletions}) ===\n";
            var body = DiffAnnotator.Annotate(f.Patch ?? "");
            var budget = _maxBatchChars - head.Length - 60;

            if (body.Length > budget)
            {
                var cut = body.LastIndexOf('\n', Math.Max(0, budget));
                body = body[..(cut > 0 ? cut : budget)] + "\n... [rest of this file's diff truncated]\n";
            }
            return head + body + "\n";
        }

        private static bool IsNoise(string path)
        {
            var lower = path.ToLowerInvariant();
            var name = lower[(lower.LastIndexOf('/') + 1)..];
            return NoiseNames.Contains(name)
                   || NoiseSuffixes.Any(lower.EndsWith)
                   || lower.Contains("/node_modules/") || lower.StartsWith("node_modules/")
                   || lower.Contains("/dist/") || lower.StartsWith("dist/");
        }
    }

}
