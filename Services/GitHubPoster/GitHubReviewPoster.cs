using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Models.Entities;
using Repository.ReviewExecutionRepository;
using Services.AIReview;
using Services.Rag;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Services.GitHubPoster
{
    public class GitHubReviewPoster : IGitHubReviewPoster
    {
        private readonly IReviewExecutionRepository _repo;
        private readonly IHttpClientFactory _http;
        private readonly ILogger<GitHubReviewPoster> _log;
        private readonly int _maxInline;
        private readonly bool _postStatus;
        private readonly string _frontendUrl;

        public GitHubReviewPoster(IReviewExecutionRepository repo, IHttpClientFactory http, IConfiguration cfg, ILogger<GitHubReviewPoster> log)
        {
            _repo = repo; _http = http; _log = log;
            _maxInline = cfg.GetValue("Review:MaxInlineComments", 25);
            _postStatus = cfg.GetValue("Review:PostCommitStatus", true);
            _frontendUrl = cfg["App:FrontendUrl"] ?? "";
        }

        public async Task<long?> PostAsync(Models.Entities.PullRequest pr, Review review, IReadOnlyList<ReviewIssue> issues, string token,
            IReadOnlyDictionary<int, string>? ruleTitles = null, CancellationToken ct = default)
        {
            if (review.GitHubReviewId != null) return review.GitHubReviewId;   // idempotent on job retry

            var repoFull = pr.Repository.FullName;
            var sha = pr.HeadSha ?? await FetchHeadShaAsync(repoFull, pr.Number, token, ct);
            if (string.IsNullOrEmpty(sha))
            {
                _log.LogWarning("PR #{N}: no head sha, cannot post to GitHub", pr.Number);
                return null;
            }

            // findings already posted in earlier versions of this review: don't spam them again
            var already = await _repo.GetPostedFingerprintsAsync(pr.Id, review.Id, ct);

            var commentable = pr.PullRequestFiles
                .GroupBy(f => f.FileName)
                .ToDictionary(g => g.Key, g => DiffAnnotator.CommentableLines(g.First().Patch), StringComparer.Ordinal);

            var inline = new List<(ReviewIssue Issue, object Comment)>();
            var inBody = new List<ReviewIssue>();
            var repeated = 0;


            foreach (var issue in issues.OrderBy(i => Rank(i.Severity)))
            {
                if (issue.Fingerprint != null && already.Contains(issue.Fingerprint)) { repeated++; continue; }

                var canInline = inline.Count < _maxInline
                                && issue.FileName != null && issue.LineNumber != null
                                && commentable.TryGetValue(issue.FileName, out var lines)
                                && lines.Contains(issue.LineNumber.Value);

                if (canInline)
                    inline.Add((issue, new Dictionary<string, object>
                    {
                        ["path"] = issue.FileName!,
                        ["line"] = issue.LineNumber!.Value,
                        ["side"] = "RIGHT",
                        ["body"] = CommentBody(issue, review.Version, RuleTitle(issue, ruleTitles))
                    }));
                else
                    inBody.Add(issue);
            }


            var url = $"https://api.github.com/repos/{repoFull}/pulls/{pr.Number}/reviews";
            var body = SummaryBody(pr, review, inBody, repeated, inline.Count);

            var (status, resp) = await Send(HttpMethod.Post, url, token, new Dictionary<string, object>
            {
                ["commit_id"] = sha,
                ["body"] = body,
                ["event"] = "COMMENT",
                ["comments"] = inline.Select(x => x.Comment).ToList()
            }, ct, attempts: 1);   // not retried: a retry after a lost response could post twice

            if ((int)status == 422 && inline.Count > 0)
            {
                // one bad line position rejects the whole review -> fall back to a body-only review
                _log.LogWarning("GitHub rejected inline comments for PR #{N}: {R}", pr.Number, HttpRetry.Truncate(resp, 300));
                inBody.AddRange(inline.Select(x => x.Issue));
                inline.Clear();
                body = SummaryBody(pr, review, inBody, repeated, 0);
                (status, resp) = await Send(HttpMethod.Post, url, token, new Dictionary<string, object>
                {
                    ["commit_id"] = sha,
                    ["body"] = body,
                    ["event"] = "COMMENT"
                }, ct, attempts: 1);
            }

            if ((int)status >= 400)
            {
                _log.LogError("GitHub review post failed {S}: {R}", (int)status, HttpRetry.Truncate(resp, 300));
                return null;
            }

            using var doc = JsonDocument.Parse(resp);
            var ghId = doc.RootElement.GetProperty("id").GetInt64();

            await _repo.MarkPostedToGitHubAsync(review.Id, ghId, inline.Select(x => x.Issue.Id).ToList(), ct);

            if (_postStatus) await PostStatusAsync(pr, review, sha, token, ct);

            _log.LogInformation("Posted GitHub review {Id} on PR #{N}: {I} inline, {B} in summary", ghId, pr.Number, inline.Count, inBody.Count);
            return ghId;
        }

        // ── commit status (use with branch protection to really block merges) ────────
        private async Task PostStatusAsync(Models.Entities.PullRequest pr, Review review, string sha, string token, CancellationToken ct)
        {
            try
            {
                var ok = review.MergeRecommendation == true;
                var desc = ok
                    ? $"Score {review.OverallScore}/100 - no high severity issues"
                    : $"Score {review.OverallScore}/100 - {review.HighIssueCount} high severity issue(s)";

                var payload = new Dictionary<string, object>
                {
                    ["state"] = ok ? "success" : "failure",
                    ["context"] = "AI Code Review",
                    ["description"] = desc.Length > 140 ? desc[..140] : desc
                };
                if (!string.IsNullOrEmpty(_frontendUrl)) payload["target_url"] = _frontendUrl.TrimEnd('/');

                var (st, resp) = await Send(HttpMethod.Post, $"https://api.github.com/repos/{pr.Repository.FullName}/statuses/{sha}", token, payload, ct);
                if ((int)st >= 400) _log.LogWarning("Commit status failed {S}: {R}", (int)st, HttpRetry.Truncate(resp, 200));
            }
            catch (Exception ex) { _log.LogWarning(ex, "Commit status failed"); }
        }

        // ── formatting ───────────────────────────────────────────────────────────────
        private static int Rank(string s) => s switch { "HIGH" => 0, "MEDIUM" => 1, "LOW" => 2, _ => 3 };
        private static string Icon(string s) => s switch { "HIGH" => "🔴", "MEDIUM" => "🟠", "LOW" => "🟡", _ => "💡" };

        private static string? RuleTitle(ReviewIssue i, IReadOnlyDictionary<int, string>? titles) =>
            i.RuleId is int id && titles != null && titles.TryGetValue(id, out var t) ? t : null;

        private static string CommentBody(ReviewIssue i, int version, string? ruleTitle)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"**{Icon(i.Severity)} {i.Severity}{(string.IsNullOrEmpty(i.Category) ? "" : " · " + i.Category)}**");
            if (ruleTitle != null) sb.AppendLine($"📏 *Team rule: {ruleTitle}*");
            sb.AppendLine();
            sb.AppendLine(i.Description);
            if (!string.IsNullOrWhiteSpace(i.Suggestion))
            {
                sb.AppendLine();
                sb.AppendLine($"💡 **Suggestion:** {i.Suggestion}");
            }
            sb.AppendLine();
            sb.Append($"<sub>AI Code Reviewer · review v{version}</sub>");
            return sb.ToString();
        }

        private static string SummaryBody(Models.Entities.PullRequest pr, Review r, List<ReviewIssue> inBody, int repeated, int inlineCount)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"## 🤖 AI Code Review · v{r.Version}");
            sb.AppendLine();
            sb.AppendLine($"| Overall | Quality | Security | Maintainability |");
            sb.AppendLine($"|:-:|:-:|:-:|:-:|");
            sb.AppendLine($"| **{r.OverallScore}/100** | {r.CodeQualityScore} | {r.SecurityScore} | {r.MaintainabilityScore} |");
            sb.AppendLine();
            sb.AppendLine(r.MergeRecommendation == true
                ? "✅ **No blocking issues found.**"
                : $"⛔ **Merge not recommended** - {r.HighIssueCount} high severity issue(s).");
            sb.AppendLine();
            sb.AppendLine($"🔴 {r.HighIssueCount} high · 🟠 {r.MediumIssueCount} medium · 🟡 {r.LowIssueCount} low · 💡 {r.SuggestionCount} suggestions" +
                          (inlineCount > 0 ? $"  —  {inlineCount} posted inline below." : ""));
            sb.AppendLine();
            if (!string.IsNullOrWhiteSpace(r.AiSummary)) { sb.AppendLine(r.AiSummary); sb.AppendLine(); }
            if (r.UsedRag && r.ContextChunkCount > 0)
                sb.AppendLine($"_Reviewed with codebase context: {r.ContextChunkCount} related code chunks from this repository._\n");

            if (inBody.Count > 0)
            {
                sb.AppendLine("### Other findings");
                foreach (var i in inBody)
                {
                    var where = i.FileName == null ? "" : $" `{i.FileName}{(i.LineNumber != null ? ":" + i.LineNumber : "")}`";
                    sb.AppendLine($"- {Icon(i.Severity)} **{i.Severity}**{where} - {i.Description}");
                }
                sb.AppendLine();
            }
            if (repeated > 0)
                sb.AppendLine($"_{repeated} issue(s) reported in an earlier review are still present and were not re-posted._");

            var text = sb.ToString();
            return text.Length > 60000 ? text[..60000] : text;
        }

        // ── http ─────────────────────────────────────────────────────────────────────
        private Task<(System.Net.HttpStatusCode Status, string Body)> Send(
            HttpMethod method, string url, string token, object? payload, CancellationToken ct, int attempts = 3)
        {
            var json = payload is null ? null : JsonSerializer.Serialize(payload);
            return HttpRetry.SendAsync(_http, () =>
            {
                var req = new HttpRequestMessage(method, url);
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                req.Headers.UserAgent.ParseAdd("AICodeReviewer");
                req.Headers.Accept.ParseAdd("application/vnd.github+json");
                req.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
                if (json != null) req.Content = new StringContent(json, Encoding.UTF8, "application/json");
                return req;
            }, _log, ct, attempts);
        }

        private async Task<string?> FetchHeadShaAsync(string repoFull, int number, string token, CancellationToken ct)
        {
            var (st, body) = await Send(HttpMethod.Get, $"https://api.github.com/repos/{repoFull}/pulls/{number}", token, null, ct);
            if ((int)st >= 400) return null;
            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.GetProperty("head").GetProperty("sha").GetString();
        }
    }

}
