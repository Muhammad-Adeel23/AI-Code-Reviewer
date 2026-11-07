using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Models.Entities;
using Models.ResponseModels.AIReview;
using Repository.Rules;
using Services.AIReview;
using Services.Rag;
using Services.Rule;
using System.Text;
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
        private readonly int _maxBatchChars, _maxTotalChars, _maxFiles, _maxRuleChars, _maxIssues, _maxRagBatches;

        public ReviewPipeline(IAiReviewService ai, IContextRetriever retriever, IReviewRuleRepository rules,
            IConfiguration cfg, ILogger<ReviewPipeline> log)
        {
            _ai = ai; _retriever = retriever; _rules = rules; _log = log;
            _maxBatchChars = cfg.GetValue("Review:MaxBatchChars", 14000);      // size of ONE LLM call
            _maxTotalChars = cfg.GetValue("Review:MaxTotalChars", 320000);     // whole-PR budget (~80k tokens)
            _maxFiles = cfg.GetValue("Review:MaxFilesPerReview", 150);         // replaces the old per-repo limit of 20
            _maxRuleChars = cfg.GetValue("Review:MaxRuleChars", 4000);
            _maxIssues = cfg.GetValue("Review:MaxIssuesPerReview", 60);
            _maxRagBatches = cfg.GetValue("Review:MaxRagBatches", 6);          // only the highest-priority batches get repo context
        }

        public async Task<PipelineResult> RunAsync(Models.Entities.PullRequest pr, bool useRag, CancellationToken ct = default)
        {
            var repo = pr.Repository;
            var noRules = (IReadOnlyDictionary<int, string>)new Dictionary<int, string>();

            var all = pr.PullRequestFiles.ToList();
            var withPatch = all.Where(f => !string.IsNullOrWhiteSpace(f.Patch)).ToList();
            var noPatch = all.Count - withPatch.Count;                          // binary or "too large" for GitHub's API
            var noise = withPatch.Where(f => IsNoise(f.FileName)).ToList();
            var candidates = withPatch.Except(noise).ToList();

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
                }, false, 0, null, new List<string>(), noRules);
            }

            // ── organization rules (never fatal) ─────────────────────────────────────
            var rules = new List<ReviewRule>();
            if (repo.OrganizationId is int orgId)
            {
                try { rules = await _rules.GetActiveForRepositoryAsync(orgId, repo.Id); }
                catch (Exception ex) { _log.LogWarning(ex, "Loading review rules failed for repo {Id}; continuing without", repo.Id); }
            }
            var scopedRules = rules.Where(r => !string.IsNullOrWhiteSpace(r.FilePattern)).ToList();

            // ── 1) rank files, most important first ──────────────────────────────────
            var ordered = candidates
                .Select((f, idx) => new
                {
                    File = f,
                    Idx = idx,
                    Score = FilePriority.Score(f.FileName, f.Status, f.Additions, f.Deletions,
                        scopedRules.Any(r => GlobMatcher.IsMatch(r.FilePattern, f.FileName)))
                })
                .OrderByDescending(x => x.Score).ThenBy(x => x.Idx)
                .Select(x => x.File)
                .ToList();

            // ── 2) pack into batches under the total budget ──────────────────────────
            var batches = new List<(List<string> Files, string Diff)>();
            var curFiles = new List<string>();
            var cur = new StringBuilder();
            var total = 0;
            var reviewed = new List<string>();
            var partlyReviewed = new List<string>();
            var omitted = new List<string>();

            void Flush()
            {
                if (cur.Length == 0) return;
                batches.Add((curFiles.Distinct().ToList(), cur.ToString()));
                curFiles = new List<string>();
                cur.Clear();
            }

            foreach (var f in ordered)
            {
                if (reviewed.Count + partlyReviewed.Count >= _maxFiles) { omitted.Add(f.FileName); continue; }

                var blocks = BuildBlocks(f);
                var added = 0;
                foreach (var block in blocks)
                {
                    if (total + block.Length > _maxTotalChars) break;
                    if (cur.Length > 0 && cur.Length + block.Length > _maxBatchChars) Flush();
                    cur.Append(block);
                    curFiles.Add(f.FileName);
                    total += block.Length;
                    added++;
                }

                if (added == 0) omitted.Add(f.FileName);
                else if (added < blocks.Count) partlyReviewed.Add(f.FileName);
                else reviewed.Add(f.FileName);
            }
            Flush();

            // ── 3) context + rules per batch (batches are in priority order) ─────────
            var request = new ReviewRequest
            {
                PrTitle = pr.Title,
                PrDescription = pr.Description,
                RepoInstructions = repo.CustomInstructions
            };
            var sources = new List<string>();
            var chunks = 0;
            var usedRules = new Dictionary<int, ReviewRule>();
            var batchNo = 0;

            foreach (var (files, diff) in batches)
            {
                batchNo++;
                string? ctx = null;
                if (useRag && batchNo <= _maxRagBatches)
                {
                    try
                    {
                        var changed = ordered.Where(f => files.Contains(f.FileName)).ToList();
                        var rc = await _retriever.BuildForFilesAsync(repo.Id, changed, ct);
                        if (!rc.IsEmpty) { ctx = rc.Text; chunks += rc.ChunkCount; sources.AddRange(rc.Sources); }
                    }
                    catch (Exception ex)
                    {
                        _log.LogWarning(ex, "Context retrieval failed for PR #{N} batch {B}; reviewing without context", pr.Number, batchNo);
                    }
                }

                var applicable = new List<RulePrompt>();
                var used = 0;
                foreach (var r in rules.Where(r => GlobMatcher.IsMatchAny(r.FilePattern, files)))
                {
                    var len = r.Title.Length + r.Description.Length + 40;
                    if (used + len > _maxRuleChars) break;
                    used += len;
                    applicable.Add(new RulePrompt(r.Id, r.Title, r.Description, r.Severity, r.Category));
                    usedRules[r.Id] = r;
                }

                request.Batches.Add(new ReviewBatch { Diff = diff, Context = ctx, Rules = applicable,  Files= files });
            }

            request.SummaryNote = BuildCoverageNote(all.Count, reviewed.Count + partlyReviewed.Count, partlyReviewed, omitted, noPatch);

            _log.LogInformation("PR #{N}: {R} reviewed, {P} partial, {O} omitted, {B} batches, {T} chars",
                pr.Number, reviewed.Count, partlyReviewed.Count, omitted.Count, batches.Count, total);

            // ── 4) LLM (parallel, fault tolerant) + merge ────────────────────────────
            var result = await _ai.ReviewAsync(request, ct);
            ApplyRules(result, usedRules);
            CapIssues(result);

            var summary = sources.Count == 0 ? null : string.Join("; ", sources.Distinct().Take(25));
            if (summary is { Length: > 1900 }) summary = summary[..1900];

            var titles = usedRules.ToDictionary(kv => kv.Key, kv => kv.Value.Title);
            return new PipelineResult(result, chunks > 0, chunks, summary, omitted, titles);
        }

        // ── helpers ──────────────────────────────────────────────────────────────────

        private static string? BuildCoverageNote(int totalFiles, int reviewedFiles, List<string> partlyReviewed, List<string> omitted, int noPatch)
        {
            if (omitted.Count == 0 && partlyReviewed.Count == 0 && noPatch == 0) return null;

            var sb = new StringBuilder($"Coverage: {reviewedFiles} of {totalFiles} changed files reviewed.");
            if (omitted.Count > 0)
                sb.Append($" Not reviewed (lowest priority / size budget): {string.Join(", ", omitted.Take(8))}{(omitted.Count > 8 ? $" and {omitted.Count - 8} more" : "")}.");
            if (partlyReviewed.Count > 0)
                sb.Append($" Only partly reviewed (very large diff): {string.Join(", ", partlyReviewed.Take(5))}.");
            if (noPatch > 0)
                sb.Append($" {noPatch} file(s) had no diff from GitHub (binary or too large).");
            return sb.ToString();
        }

        /// <summary>Keeps the most severe findings if the model produced too many across all batches.</summary>
        private void CapIssues(AiReviewResult result)
        {
            if (result.Issues.Count <= _maxIssues) return;
            var dropped = result.Issues.Count - _maxIssues;
            result.Issues = result.Issues.OrderBy(i => Rank(i.Severity)).Take(_maxIssues).ToList();
            result.AiSummary = $"{result.AiSummary}\n\n{dropped} lower-severity finding(s) were omitted to keep the report readable.".Trim();
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

        /// <summary>One file -> one or more blocks. Big diffs are split at hunk boundaries (line numbers stay correct).</summary>
        private List<string> BuildBlocks(PullRequestFile f)
        {
            var header = $"=== {f.FileName} ({f.Status}, +{f.Additions} -{f.Deletions})";
            var annotated = DiffAnnotator.Annotate(f.Patch ?? "").Replace("\r\n", "\n");
            var budget = Math.Max(2000, _maxBatchChars - header.Length - 80);

            if (annotated.Length <= budget)
                return new List<string> { $"{header} ===\n{annotated}\n" };

            var parts = new List<string>();
            var cur = new StringBuilder();

            foreach (var hunk in SplitAtHunks(annotated))
            {
                if (cur.Length > 0 && cur.Length + hunk.Length > budget)
                {
                    parts.Add(cur.ToString());
                    cur.Clear();
                }

                if (hunk.Length > budget)                       // a single enormous hunk: cut by lines
                {
                    if (cur.Length > 0) { parts.Add(cur.ToString()); cur.Clear(); }
                    var piece = new StringBuilder();
                    foreach (var line in hunk.Split('\n'))
                    {
                        if (piece.Length > 0 && piece.Length + line.Length + 1 > budget)
                        {
                            parts.Add(piece.ToString());
                            piece.Clear();
                        }
                        piece.Append(line).Append('\n');
                    }
                    if (piece.Length > 0) cur.Append(piece);
                }
                else cur.Append(hunk);
            }
            if (cur.Length > 0) parts.Add(cur.ToString());

            return parts.Select((p, i) => $"{header} part {i + 1}/{parts.Count} ===\n{p}\n").ToList();
        }

        private static IEnumerable<string> SplitAtHunks(string annotated)
        {
            var cur = new StringBuilder();
            foreach (var line in annotated.Split('\n'))
            {
                if (line.StartsWith("@@") && cur.Length > 0)
                {
                    yield return cur.ToString();
                    cur.Clear();
                }
                cur.Append(line).Append('\n');
            }
            if (cur.Length > 0) yield return cur.ToString();
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
