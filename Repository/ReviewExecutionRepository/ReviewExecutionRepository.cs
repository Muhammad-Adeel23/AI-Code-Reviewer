using Microsoft.EntityFrameworkCore;
using Models.Entities;

namespace Repository.ReviewExecutionRepository
{
    public class ReviewExecutionRepository : IReviewExecutionRepository
    {
        private readonly AppDbContext _db;
        public ReviewExecutionRepository(AppDbContext db) => _db = db;

        public async Task<Models.Entities.PullRequest> GetPullRequestForReviewAsync(int prId, int? organizationId = null, CancellationToken ct = default)
        {
            var q = _db.PullRequests
                .Include(p => p.Repository).ThenInclude(r => r.Owner)
                .Include(p => p.PullRequestFiles)
                .Where(p => p.Id == prId);

            if (organizationId.HasValue)
                q = q.Where(p => p.Repository.OrganizationId == organizationId.Value);

            return await q.FirstOrDefaultAsync(ct);
        }

        public async Task<Review> AcquireReviewAsync(int prId, string? jobId)
        {
            if (jobId != null)
            {
                var existing = await _db.Reviews.FirstOrDefaultAsync(r => r.PullRequestId == prId && r.HangfireJobId == jobId);
                if (existing != null)
                {
                    existing.Status = "Processing";
                    existing.ErrorMessage = null;
                    existing.RetryCount++;
                    await _db.SaveChangesAsync();
                    return existing;
                }
            }

            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    var version = (await _db.Reviews.Where(r => r.PullRequestId == prId)
                                       .MaxAsync(r => (int?)r.Version) ?? 0) + 1;
                    var review = new Review
                    {
                        PullRequestId = prId,
                        Version = version,
                        Status = "Processing",
                        HangfireJobId = jobId,
                        CreatedAt = DateTime.UtcNow
                    };
                    _db.Reviews.Add(review);
                    await _db.SaveChangesAsync();
                    return review;
                }
                catch (DbUpdateException) when (attempt < 3)
                {
                    // unique (PullRequestId, Version) hit by a concurrent job -> forget the failed insert and retry
                    foreach (var e in _db.ChangeTracker.Entries<Review>().Where(x => x.State == EntityState.Added).ToList())
                        e.State = EntityState.Detached;
                }
            }
        }

        public async Task MarkAnalyzingAsync(int prId)
        {
            var pr = await _db.PullRequests.FirstAsync(p => p.Id == prId);
            pr.ReviewStatus = "Analyzing";
            pr.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        public async Task<CompletedReview> CompleteReviewAsync(ReviewCompletion c)
        {
            var review = await _db.Reviews.FirstAsync(r => r.Id == c.ReviewId);
            var pr = await _db.PullRequests.FirstAsync(p => p.Id == c.PullRequestId);

            var issues = c.Issues.Select(i => new ReviewIssue
            {
                ReviewId = review.Id,
                Severity = i.Severity,
                FileName = i.FileName,
                LineNumber = i.LineNumber,
                Description = i.Description,
                Suggestion = i.Suggestion,
                Category = i.Category,
                Fingerprint = i.Fingerprint,
                RuleId = i.RuleId,
                Status = "Open",
                CreatedAt = DateTime.UtcNow
            }).ToList();
            _db.ReviewIssues.AddRange(issues);

            review.Status = "Completed";
            review.CodeQualityScore = c.CodeQuality;
            review.SecurityScore = c.Security;
            review.MaintainabilityScore = c.Maintainability;
            review.OverallScore = c.Overall;
            review.MergeRecommendation = c.MergeRecommendation;
            review.AiSummary = c.Summary;
            review.HighIssueCount = c.High;
            review.MediumIssueCount = c.Medium;
            review.LowIssueCount = c.Low;
            review.SuggestionCount = c.Suggestions;
            review.PromptTokensUsed = c.PromptTokens;
            review.CompletionTokensUsed = c.CompletionTokens;
            review.ProcessingMs = c.ProcessingMs;
            review.ReviewedAt = DateTime.UtcNow;
            review.ErrorMessage = null;
            review.UsedRag = c.UsedRag;
            review.ContextChunkCount = c.ContextChunks;
            review.ContextSummary = c.ContextSummary;
            await _db.SaveChangesAsync();

            // a slower, older job must not overwrite the numbers of a newer review
            if (c.Version >= pr.CurrentReviewVersion)
            {
                pr.ReviewStatus = "Completed";
                pr.MergeRecommendation = c.MergeRecommendation;
                pr.LatestAiScore = c.Overall;
                pr.HighIssueCount = c.High;
                pr.MediumIssueCount = c.Medium;
                pr.LowIssueCount = c.Low;
                pr.CurrentReviewVersion = c.Version;
                pr.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();
            }

            return new CompletedReview(review, issues);
        }

        public async Task FailReviewAsync(int reviewId, int prId, string message)
        {
            var review = await _db.Reviews.FirstOrDefaultAsync(r => r.Id == reviewId);
            if (review != null)
            {
                review.Status = "Failed";
                review.ErrorMessage = message.Length > 480 ? message[..480] : message;   // column is NVARCHAR(500)
            }
            var pr = await _db.PullRequests.FirstOrDefaultAsync(p => p.Id == prId);
            if (pr != null)
            {
                pr.ReviewStatus = "Failed";
                pr.UpdatedAt = DateTime.UtcNow;
            }
            await _db.SaveChangesAsync();
        }

        public async Task<bool> WantsReviewEmailAsync(int userId) =>
            await _db.UserSettings.Where(s => s.UserId == userId)
                .Select(s => (bool?)s.EmailOnReviewComplete).FirstOrDefaultAsync() ?? true;


        public async Task<HashSet<string>> GetPostedFingerprintsAsync(int prId, int excludeReviewId, CancellationToken ct = default) =>
       (await _db.ReviewIssues
           .Where(i => i.Review.PullRequestId == prId && i.ReviewId != excludeReviewId && i.PostedInline && i.Fingerprint != null)
           .Select(i => i.Fingerprint!)
           .ToListAsync(ct)).ToHashSet();
        public async Task MarkPostedToGitHubAsync(int reviewId, long githubReviewId, IReadOnlyCollection<int> inlineIssueIds, CancellationToken ct = default)
        {
            var review = await _db.Reviews.FirstAsync(r => r.Id == reviewId, ct);
            review.GitHubReviewId = githubReviewId;
            review.GitHubPostedAt = DateTime.UtcNow;

            if (inlineIssueIds.Count > 0)
            {
                var issues = await _db.ReviewIssues.Where(i => inlineIssueIds.Contains(i.Id)).ToListAsync(ct);
                foreach (var i in issues) i.PostedInline = true;
            }
            await _db.SaveChangesAsync(ct);
        }
    }
}
