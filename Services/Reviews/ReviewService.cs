using Microsoft.Extensions.Logging;
using Models.Entities;
using Repository.AIReview;
using Repository.PullRequest;
using Repository.Teams;
using static Models.DTOs.AIReviewDTO;

namespace Services.Reviews
{
    /// <summary>
    /// v2: every method now checks that the PR belongs to the caller's organisation.
    /// v1 only checked authentication, so any logged-in user could read reviews / change issue status
    /// of ANY organisation by guessing integer ids (IDOR). We answer 404 (not 403/401) so ids can't be probed,
    /// and because GlobalExceptionMiddleware maps UnauthorizedAccessException to 401 (which makes the SPA log the user out).
    /// </summary>
    public class ReviewService : IReviewService
    {
        private readonly IReviewRepository _reviewRepo;
        private readonly IPullRequestRepository _prRepo;
        private readonly ITeamRepository _teamRepo;
        private readonly ILogger<ReviewService> _logger;

        public ReviewService(IReviewRepository reviewRepo, IPullRequestRepository prRepo,
            ITeamRepository teamRepo, ILogger<ReviewService> logger)
        {
            _reviewRepo = reviewRepo; _prRepo = prRepo; _teamRepo = teamRepo; _logger = logger;
        }

        private async Task EnsurePrAccessAsync(int userId, int prId)
        {
            var org = await _teamRepo.GetOrganizationByUserAsync(userId);
            if (org is null || !await _prRepo.IsPrInOrgAsync(prId, org.Id))
                throw new KeyNotFoundException("PR nahi mili");
        }

        public async Task<ReviewDetailDto> GetLatestReviewAsync(int userId, int prId)
        {
            await EnsurePrAccessAsync(userId, prId);

            var review = await _reviewRepo.GetLatestByPrAsync(prId)
                ?? throw new KeyNotFoundException("Review nahi mili");

            return MapToDetailDto(review);
        }

        public async Task<List<ReviewHistoryDto>> GetReviewHistoryAsync(int userId, int prId)
        {
            await EnsurePrAccessAsync(userId, prId);

            var reviews = await _reviewRepo.GetHistoryByPrAsync(prId);
            return reviews.Select((r, index) => new ReviewHistoryDto
            {
                Version = r.Version,
                OverallScore = r.OverallScore,
                CodeQualityScore = r.CodeQualityScore,
                SecurityScore = r.SecurityScore,
                MaintainabilityScore = r.MaintainabilityScore,
                HighIssueCount = r.HighIssueCount,
                MediumIssueCount = r.MediumIssueCount,
                LowIssueCount = r.LowIssueCount,
                MergeRecommendation = r.MergeRecommendation,
                ReviewedAt = r.ReviewedAt,
                ScoreImprovement = index > 0 ? r.OverallScore - reviews[index - 1].OverallScore : null
            }).ToList();
        }

        public async Task UpdateIssueStatusAsync(int userId, int issueId, string status)
        {
            var validStatuses = new[] { "Open", "Fixed", "Acknowledged" };
            if (!validStatuses.Contains(status))
                throw new ArgumentException("Invalid status");

            // needs IReviewRepository.GetPrIdByIssueAsync - see PATCHES.md (2-line addition)
            var prId = await _reviewRepo.GetPrIdByIssueAsync(issueId)
                ?? throw new KeyNotFoundException("Issue nahi mili");

            await EnsurePrAccessAsync(userId, prId);
            await _reviewRepo.UpdateIssueStatusAsync(issueId, status, userId);
        }

        private static ReviewDetailDto MapToDetailDto(Review review) =>
            new()
            {
                Id = review.Id,
                Version = review.Version,
                CodeQualityScore = review.CodeQualityScore,
                SecurityScore = review.SecurityScore,
                MaintainabilityScore = review.MaintainabilityScore,
                OverallScore = review.OverallScore,
                MergeRecommendation = review.MergeRecommendation,
                AiSummary = review.AiSummary,
                HighIssueCount = review.HighIssueCount,
                MediumIssueCount = review.MediumIssueCount,
                LowIssueCount = review.LowIssueCount,
                ReviewedAt = review.ReviewedAt,
                Issues = review.ReviewIssues.Select(i => new ReviewIssueDto
                {
                    Id = i.Id,
                    Severity = i.Severity,
                    FileName = i.FileName,
                    LineNumber = i.LineNumber,
                    Description = i.Description,
                    Suggestion = i.Suggestion,
                    Category = i.Category,
                    RuleId = i.RuleId,
                    Status = i.Status
                }).ToList()
            };
    }
}
