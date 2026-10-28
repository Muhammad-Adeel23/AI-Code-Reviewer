using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Models.DTOs
{
    public class PullRequestDTO
    {
        public class PullRequestListDto
        {
            public int Id { get; set; }
            public int Number { get; set; }
            public string Title { get; set; } = string.Empty;
            public string AuthorGitHubLogin { get; set; } = string.Empty;
            public string? AuthorAvatarUrl { get; set; }
            public string SourceBranch { get; set; } = string.Empty;
            public string TargetBranch { get; set; } = string.Empty;
            public string Status { get; set; } = string.Empty;
            public string ReviewStatus { get; set; } = string.Empty;
            public int HighIssueCount { get; set; }
            public int MediumIssueCount { get; set; }
            public int LowIssueCount { get; set; }
            public int? LatestAiScore { get; set; }
            public bool? MergeRecommendation { get; set; }
            public int CurrentReviewVersion { get; set; }
            public string GitHubPrUrl { get; set; } = string.Empty;
            public string RepoName { get; set; } = string.Empty;
            public int RepoId { get; set; }
            public DateTime GitHubCreatedAt { get; set; }
            public DateTime GitHubUpdatedAt { get; set; }
        }
        public class PullRequestDetailDto
        {
            public int Id { get; set; }
            public int Number { get; set; }
            public string Title { get; set; } = string.Empty;
            public string? Description { get; set; }
            public string AuthorGitHubLogin { get; set; } = string.Empty;
            public string? AuthorAvatarUrl { get; set; }
            public string SourceBranch { get; set; } = string.Empty;
            public string TargetBranch { get; set; } = string.Empty;
            public string Status { get; set; } = string.Empty;
            public string ReviewStatus { get; set; } = string.Empty;
            public bool? MergeRecommendation { get; set; }
            public int CurrentReviewVersion { get; set; }
            public int HighIssueCount { get; set; }
            public int MediumIssueCount { get; set; }
            public int LowIssueCount { get; set; }
            public int? LatestAiScore { get; set; }
            public int TotalAdditions { get; set; }
            public int TotalDeletions { get; set; }
            public int ChangedFilesCount { get; set; }
            public string GitHubPrUrl { get; set; } = string.Empty;
            public DateTime GitHubCreatedAt { get; set; }
            public DateTime? MergedAt { get; set; }
            public RepoInfoDto Repository { get; set; } = null!;
            public List<PullRequestFileDto> ChangedFiles { get; set; } = new();
            public ReviewSummaryDto? LatestReview { get; set; }
        }

        public class PullRequestFileDto
        {
            public string FileName { get; set; } = string.Empty;
            public string Status { get; set; } = string.Empty;
            public int Additions { get; set; }
            public int Deletions { get; set; }
            public string? Patch { get; set; }
        }

        public class RepoInfoDto
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public string FullName { get; set; } = string.Empty;
        }

        public class ReviewSummaryDto
        {
            public int Id { get; set; }
            public int Version { get; set; }
            public int? CodeQualityScore { get; set; }
            public int? SecurityScore { get; set; }
            public int? MaintainabilityScore { get; set; }
            public int? OverallScore { get; set; }
            public bool? MergeRecommendation { get; set; }
            public string? AiSummary { get; set; }
            public int HighIssueCount { get; set; }
            public int MediumIssueCount { get; set; }
            public int LowIssueCount { get; set; }
            public DateTime? ReviewedAt { get; set; }
        }

        // ─── PR Filter Request ────────────────────────────────────────────────────────
        public class PrFilterRequest
        {
            public string? Status { get; set; } // Open|Closed|Merged|All
            public string? ReviewStatus { get; set; } // Pending|Queued|Analyzing|Completed|Failed|All
            public string? Search { get; set; } // Title/Author search
            public int? RepoId { get; set; } // Specific repo filter
            public int Page { get; set; } = 1;
            public int PageSize { get; set; } = 20;
        }

        // ─── Paged Result ─────────────────────────────────────────────────────────────
        public class PagedResult<T>
        {
            public List<T> Data { get; set; } = new();
            public int Total { get; set; }
            public int Page { get; set; }
            public int PageSize { get; set; }
            public int TotalPages => (int)Math.Ceiling((double)Total / PageSize);
        }

        // ─── Review Status (polling ke liye) ─────────────────────────────────────────
        public class ReviewStatusDto
        {
            public string Status { get; set; } = string.Empty;
            public int Progress { get; set; }
            public string CurrentStep { get; set; } = string.Empty;
            public string? JobId { get; set; }
        }
    }
}
