using Components.Common;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Models.Entities;
using Models.ResponseModels.AIReview;
using Repository.AIReview;
using Repository.Email;
using Repository.SignalRNotifier;
using Services.AIReview;
using Services.AiReviewJob;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Services.AiReviewJob
{
    public class AiReviewJob : IAiReviewJob
    {
        private readonly AppDbContext _db;
        private readonly IAiReviewService _aiService;
        private readonly IReviewRepository _reviewRepo;
        private readonly ISignalRNotifier _signalR;
        private readonly IEmailSenderRepository _emailRepo;
        private readonly ILogger<AiReviewJob> _logger;

        public AiReviewJob(
            AppDbContext db,
            IAiReviewService aiService,
            IReviewRepository reviewRepo,
            ISignalRNotifier signalR,
            IEmailSenderRepository emailRepo,
            ILogger<AiReviewJob> logger)
        {
            _db = db;
            _aiService = aiService;
            _reviewRepo = reviewRepo;
            _signalR = signalR;
            _emailRepo = emailRepo;
            _logger = logger;
        }

        [AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 60, 120, 300 })]
        public async Task ExecuteAsync(int pullRequestId)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            // 1. PR fetch karo
            var pr = await _db.PullRequests
                .Include(p => p.Repository)
                    .ThenInclude(r => r.Owner)
                .Include(p => p.PullRequestFiles)
                .FirstOrDefaultAsync(p => p.Id == pullRequestId);

            if (pr is null)
            {
                _logger.LogError("PR not found: {Id}", pullRequestId);
                return;
            }

            var ownerId = pr.Repository.OwnerId;
            _logger.LogInformation(
                "AI Review starting — PR #{Num} | Repo: {Repo}",
                pr.Number, pr.Repository.FullName);

            // 2. Review record create karo
            var version = await _reviewRepo.GetNextVersionAsync(pullRequestId);
            var review = await _reviewRepo.CreateAsync(new Review
            {
                PullRequestId = pullRequestId,
                Version = version,
                Status = "Processing",
                CreatedAt = DateTime.UtcNow
            });

            // 3. PR status update karo
            pr.ReviewStatus = "Analyzing";
            pr.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            // 4. SignalR — review started notify karo
            await _signalR.NotifyReviewStartedAsync(
                ownerId, pr.Id, pr.Number, pr.Repository.Name);

            try
            {
                // 5. Diff prepare karo
                var diff = BuildDiff(pr.PullRequestFiles.ToList());

                if (string.IsNullOrEmpty(diff))
                {
                    _logger.LogWarning("PR #{Num} mein koi diff nahi", pr.Number);
                    diff = "No code changes found in this PR.";
                }

                // 6. Claude API call karo
                var aiResult = await _aiService.ReviewCodeAsync(
                    diff, pr.Title, pr.Description);

                // 7. Issues save karo
                foreach (var issue in aiResult.Issues)
                {
                    _db.ReviewIssues.Add(new ReviewIssue
                    {
                        ReviewId = review.Id,
                        Severity = issue.Severity,
                        FileName = issue.FileName,
                        LineNumber = issue.LineNumber,
                        Description = issue.Description,
                        Suggestion = issue.Suggestion,
                        Category = issue.Category,
                        Status = "Open",
                        CreatedAt = DateTime.UtcNow
                    });
                }

                // 8. Review update karo
                var highCount = aiResult.Issues.Count(i => i.Severity == "HIGH");
                var medCount = aiResult.Issues.Count(i => i.Severity == "MEDIUM");
                var lowCount = aiResult.Issues.Count(i => i.Severity == "LOW");
                var sugCount = aiResult.Issues.Count(i => i.Severity == "SUGGESTION");

                stopwatch.Stop();

                review.Status = "Completed";
                review.CodeQualityScore = aiResult.CodeQualityScore;
                review.SecurityScore = aiResult.SecurityScore;
                review.MaintainabilityScore = aiResult.MaintainabilityScore;
                review.OverallScore = aiResult.OverallScore;
                review.MergeRecommendation = aiResult.MergeRecommendation;
                review.AiSummary = aiResult.AiSummary;
                review.HighIssueCount = highCount;
                review.MediumIssueCount = medCount;
                review.LowIssueCount = lowCount;
                review.SuggestionCount = sugCount;
                review.PromptTokensUsed = aiResult.PromptTokens;
                review.CompletionTokensUsed = aiResult.CompletionTokens;
                review.ProcessingMs = (int)stopwatch.ElapsedMilliseconds;
                review.ReviewedAt = DateTime.UtcNow;

                await _db.SaveChangesAsync();

                // 9. PR update karo
                pr.ReviewStatus = "Completed";
                pr.MergeRecommendation = aiResult.MergeRecommendation;
                pr.LatestAiScore = aiResult.OverallScore;
                pr.HighIssueCount = highCount;
                pr.MediumIssueCount = medCount;
                pr.LowIssueCount = lowCount;
                pr.CurrentReviewVersion = version;
                pr.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();

                // 10. SignalR — review complete notify karo
                await _signalR.NotifyReviewCompletedAsync(
                    ownerId, pr.Id, pr.Number, pr.Repository.Name,
                    aiResult.OverallScore, highCount, aiResult.MergeRecommendation);

                // 11. Email queue karo
                await QueueReviewCompleteEmailAsync(pr, review, aiResult);

                _logger.LogInformation(
                    "Review completed — PR #{Num} | Score: {Score} | Issues: {H}H {M}M {L}L",
                    pr.Number, aiResult.OverallScore, highCount, medCount, lowCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Review failed — PR #{Num}", pr.Number);

                // Review failed update karo
                review.Status = "Failed";
                review.ErrorMessage = ex.Message;
                await _db.SaveChangesAsync();

                // PR status update
                pr.ReviewStatus = "Failed";
                pr.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();

                // SignalR notify
                await _signalR.NotifyReviewFailedAsync(
                    ownerId, pr.Id, pr.Number, ex.Message);

                throw; // Hangfire retry ke liye
            }
        }

        // ─── Diff Build karo from PullRequestFiles ────────────────────────────────

        private static string BuildDiff(List<PullRequestFile> files)
        {
            if (!files.Any()) return string.Empty;

            var sb = new System.Text.StringBuilder();

            foreach (var file in files.Take(20)) // Max 20 files
            {
                sb.AppendLine($"=== {file.FileName} ({file.Status}) ===");
                sb.AppendLine($"+{file.Additions} -{file.Deletions}");

                if (!string.IsNullOrEmpty(file.Patch))
                {
                    // Patch too large hone par truncate karo
                    var patch = file.Patch.Length > 5000
                        ? file.Patch[..5000] + "\n... (truncated)"
                        : file.Patch;

                    sb.AppendLine(patch);
                }

                sb.AppendLine();
            }

            return sb.ToString();
        }

        // ─── Email Queue ──────────────────────────────────────────────────────────

        private async Task QueueReviewCompleteEmailAsync(
            Models.Entities.PullRequest pr, Review review, AiReviewResult result)
        {
            try
            {
                var owner = pr.Repository.Owner;
                if (owner is null) return;

                var template = await _emailRepo.GetEmailTemplateById(
                    (long)EmailTemplateEnum.ReviewComplete);

                if (template is null) return;

                var body = template.TemplateBody
                    .Replace("{{UserName}}", owner.FullName)
                    .Replace("{{PrNumber}}", pr.Number.ToString())
                    .Replace("{{PrTitle}}", pr.Title)
                    .Replace("{{RepoName}}", pr.Repository.Name)
                    .Replace("{{Score}}", result.OverallScore.ToString())
                    .Replace("{{HighCount}}", result.Issues.Count(i => i.Severity == "HIGH").ToString())
                    .Replace("{{ReviewLink}}", $"{pr.GitHubPrUrl}");

                await _emailRepo.QueueEmailAsync(
                    owner.Email,
                    template.Subject,
                    body,
                    (long)EmailTemplateEnum.ReviewComplete);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Email queue karne mein masla");
                // Email fail hone par review fail mat karo
            }
        }
    }
}
