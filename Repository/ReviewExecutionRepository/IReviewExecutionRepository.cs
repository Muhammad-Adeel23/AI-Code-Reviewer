using Models.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Repository.ReviewExecutionRepository
{
    public record NewReviewIssue(string Severity, string? FileName, int? LineNumber, string Description,
    string? Suggestion, string? Category, string Fingerprint, int? RuleId);

    public record ReviewCompletion(
        int ReviewId, int PullRequestId, int Version,
        int CodeQuality, int Security, int Maintainability, int Overall, bool MergeRecommendation, string? Summary,
        int High, int Medium, int Low, int Suggestions,
        int PromptTokens, int CompletionTokens, int ProcessingMs,
        bool UsedRag, int ContextChunks, string? ContextSummary,
        IReadOnlyList<NewReviewIssue> Issues);

    public record CompletedReview(Review Review, List<ReviewIssue> Issues);
    public interface IReviewExecutionRepository
    {
        /// <summary>PR + Repository + Owner + files. Pass organizationId to enforce tenancy (eval); null for system jobs.</summary>
        Task<Models.Entities.PullRequest?> GetPullRequestForReviewAsync(int prId, int? organizationId = null, CancellationToken ct = default);

        /// <summary>Reuses the Review row of the same Hangfire job on retry, else creates the next version.</summary>
        Task<Review> AcquireReviewAsync(int prId, string? jobId);
        Task MarkAnalyzingAsync(int prId);
        Task<CompletedReview> CompleteReviewAsync(ReviewCompletion completion);
        Task FailReviewAsync(int reviewId, int prId, string message);

        Task<bool> WantsReviewEmailAsync(int userId);

        // GitHub posting
        Task<HashSet<string>> GetPostedFingerprintsAsync(int prId, int excludeReviewId, CancellationToken ct = default);
        Task MarkPostedToGitHubAsync(int reviewId, long githubReviewId, IReadOnlyCollection<int> inlineIssueIds, CancellationToken ct = default);
    }

}
