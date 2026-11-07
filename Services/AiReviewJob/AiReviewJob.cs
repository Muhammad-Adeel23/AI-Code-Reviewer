using Components.Common;
using Hangfire;
using Hangfire.Server;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Models.Entities;
using Models.ResponseModels.AIReview;
using Org.BouncyCastle.Bcpg.Sig;
using Repository.AIReview;
using Repository.Email;
using Repository.ReviewExecutionRepository;
using Repository.SignalRNotifier;
using Services.AIReview;
using Services.AiReviewJob;
using Services.GitHubPoster;
using Services.ReviewPipeline;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Services.AiReviewJob
{
    public class AiReviewJob : IAiReviewJob
    {
        private readonly IReviewExecutionRepository _repo;
        private readonly IReviewPipeline _pipeline;
        private readonly ISignalRNotifier _signalR;
        private readonly IEmailSenderRepository _emailRepo;
        private readonly IGitHubReviewPoster _poster;
        private readonly ILogger<AiReviewJob> _logger;
        private readonly bool _postToGitHub;
        private readonly bool _ragEnabled;

        public AiReviewJob(IReviewExecutionRepository repo, IReviewPipeline pipeline, ISignalRNotifier signalR,
            IEmailSenderRepository emailRepo, IGitHubReviewPoster poster, IConfiguration cfg, ILogger<AiReviewJob> logger)
        {
            _repo = repo; _pipeline = pipeline; _signalR = signalR; _emailRepo = emailRepo;
            _poster = poster; _logger = logger;
            _postToGitHub = cfg.GetValue("Review:PostToGitHub", true);
            _ragEnabled = cfg.GetValue("Rag:Enabled", true);
        }

        public async Task ExecuteAsync(int pullRequestId, PerformContext? context)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var jobId = context?.BackgroundJob?.Id;

            var pr = await _repo.GetPullRequestForReviewAsync(pullRequestId);
            if (pr is null)
            {
                _logger.LogError("PR not found: {Id}", pullRequestId);
                return;
            }

            var repo = pr.Repository;
            var targets = NotifyTargets(pr);
            _logger.LogInformation("AI Review starting - PR #{Num} | Repo: {Repo}", pr.Number, repo.FullName);

            var review = await _repo.AcquireReviewAsync(pr.Id, jobId);
            await _repo.MarkAnalyzingAsync(pr.Id);

            foreach (var uid in targets)
                await SafeNotify(() => _signalR.NotifyReviewStartedAsync(uid, pr.Id, pr.Number, repo.Name));

            try
            {
                var piped = await _pipeline.RunAsync(pr, _ragEnabled && repo.UseRag);
                var ai = piped.Result;

                // ── sanitize + apply repo severity threshold ────────────────────────
                var kept = ai.Issues
                    .Where(i => !string.IsNullOrWhiteSpace(i.Description))
                    .Where(i => Rank(i.Severity) <= ThresholdRank(repo.SeverityThreshold))
                    .ToList();

                var high = kept.Count(i => i.Severity == "HIGH");
                var med = kept.Count(i => i.Severity == "MEDIUM");
                var low = kept.Count(i => i.Severity == "LOW");
                var sug = kept.Count(i => i.Severity == "SUGGESTION");

                // The model's own boolean is only advisory: a HIGH issue ALWAYS blocks.
                var merge = ai.MergeRecommendation && high == 0;

                var newIssues = kept.Select(i => new NewReviewIssue(
                    i.Severity,
                    Cut(i.FileName, 500),
                    i.LineNumber is > 0 ? i.LineNumber : null,
                    Cut(i.Description, 1000)!,
                    Cut(i.Suggestion, 1000),
                    Cut(i.Category, 50),
                    IssueFingerprint.Compute(i.FileName, i.Category, i.Description),
                    i.RuleId)).ToList();

                sw.Stop();
                var done = await _repo.CompleteReviewAsync(new ReviewCompletion(
                    review.Id, pr.Id, review.Version,
                    ai.CodeQualityScore, ai.SecurityScore, ai.MaintainabilityScore, ai.OverallScore, merge, ai.AiSummary,
                    high, med, low, sug,
                    ai.PromptTokens, ai.CompletionTokens, (int)sw.ElapsedMilliseconds,
                    piped.UsedRag, piped.ContextChunks, piped.ContextSummary, newIssues));

                // ── GitHub (non-fatal) ──────────────────────────────────────────────
                var token = repo.Owner?.GitHubAccessToken;
                if (_postToGitHub && !string.IsNullOrEmpty(token))
                {
                    try { await _poster.PostAsync(pr, done.Review, done.Issues, token, piped.RuleTitles); }
                    catch (Exception ex) { _logger.LogError(ex, "Posting to GitHub failed for PR #{Num}", pr.Number); }
                }

                foreach (var uid in targets)
                    await SafeNotify(() => _signalR.NotifyReviewCompletedAsync(
                        uid, pr.Id, pr.Number, repo.Name, ai.OverallScore, high, merge));

                await QueueReviewCompleteEmailAsync(pr, ai.OverallScore, high);

                _logger.LogInformation("Review completed - PR #{Num} v{V} | Score {Score} | {H}H {M}M {L}L | rag={Rag} chunks={C}",
                    pr.Number, review.Version, ai.OverallScore, high, med, low, piped.UsedRag, piped.ContextChunks);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Review failed - PR #{Num}", pr.Number);
                try { await _repo.FailReviewAsync(review.Id, pr.Id, ex.Message); }
                catch (Exception inner) { _logger.LogError(inner, "Could not persist failure state"); }

                foreach (var uid in targets)
                    await SafeNotify(() => _signalR.NotifyReviewFailedAsync(uid, pr.Id, pr.Number, Cut(ex.Message, 200) ?? "Review failed"));

                throw;   // let Hangfire retry (max 2 attempts, see IAiReviewJob)
            }
        }

        // ── helpers ──────────────────────────────────────────────────────────────

        private static List<int> NotifyTargets(Models.Entities.PullRequest pr)
        {
            var ids = new List<int> { pr.Repository.OwnerId };
            if (pr.AuthorUserId is int author && author != pr.Repository.OwnerId) ids.Add(author);
            return ids;
        }

        private async Task SafeNotify(Func<Task> send)
        {
            try { await send(); } catch (Exception ex) { _logger.LogWarning(ex, "SignalR notify failed"); }
        }

        private static int Rank(string s) => s switch { "HIGH" => 0, "MEDIUM" => 1, "LOW" => 2, _ => 3 };

        private static int ThresholdRank(string? t) => (t ?? "ALL").ToUpperInvariant() switch
        {
            "HIGH" => 0,
            "MEDIUM" => 1,
            "LOW" => 2,
            _ => 3   // "ALL" / "SUGGESTION" / unknown -> keep everything
        };

        private static string? Cut(string? s, int max) =>
            string.IsNullOrEmpty(s) ? s : (s.Length <= max ? s : s[..(max - 3)] + "...");

        private async Task QueueReviewCompleteEmailAsync(Models.Entities.PullRequest pr, int score, int high)
        {
            try
            {
                var owner = pr.Repository.Owner;
                if (owner is null) return;
                if (!await _repo.WantsReviewEmailAsync(owner.Id)) return;

                var template = await _emailRepo.GetEmailTemplateById((long)EmailTemplateEnum.ReviewComplete);
                if (template is null) return;

                var body = template.TemplateBody
                    .Replace("{{UserName}}", owner.FullName)
                    .Replace("{{PrNumber}}", pr.Number.ToString())
                    .Replace("{{PrTitle}}", pr.Title)
                    .Replace("{{RepoName}}", pr.Repository.Name)
                    .Replace("{{Score}}", score.ToString())
                    .Replace("{{HighCount}}", high.ToString())
                    .Replace("{{ReviewLink}}", pr.GitHubPrUrl);

                await _emailRepo.QueueEmailAsync(owner.Email, template.Subject, body, (long)EmailTemplateEnum.ReviewComplete);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Email queue failed");   // never fail the review because of email
            }
        }
    }
}
