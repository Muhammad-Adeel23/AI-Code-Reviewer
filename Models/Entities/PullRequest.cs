using System;
using System.Collections.Generic;

namespace Models.Entities;

public partial class PullRequest
{
    public int Id { get; set; }

    public int RepositoryId { get; set; }

    public long GitHubPrId { get; set; }

    public int Number { get; set; }

    public string Title { get; set; } = null!;

    public string? Description { get; set; }

    public string AuthorGitHubLogin { get; set; } = null!;

    public string? AuthorAvatarUrl { get; set; }

    public int? AuthorUserId { get; set; }

    public string SourceBranch { get; set; } = null!;

    public string TargetBranch { get; set; } = null!;

    public string GitHubPrUrl { get; set; } = null!;

    public string Status { get; set; } = null!;

    public string ReviewStatus { get; set; } = null!;

    public bool? MergeRecommendation { get; set; }

    public int CurrentReviewVersion { get; set; }

    public int HighIssueCount { get; set; }

    public int MediumIssueCount { get; set; }

    public int LowIssueCount { get; set; }

    public int? LatestAiScore { get; set; }

    public int TotalAdditions { get; set; }

    public int TotalDeletions { get; set; }

    public int ChangedFilesCount { get; set; }

    public DateTime GitHubCreatedAt { get; set; }

    public DateTime GitHubUpdatedAt { get; set; }

    public DateTime? MergedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public string? HeadSha { get; set; }

    public virtual ICollection<ActivityLog> ActivityLogs { get; set; } = new List<ActivityLog>();

    public virtual User? AuthorUser { get; set; }

    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();

    public virtual ICollection<PullRequestFile> PullRequestFiles { get; set; } = new List<PullRequestFile>();

    public virtual Repository Repository { get; set; } = null!;

    public virtual ICollection<ReviewerAssignment> ReviewerAssignments { get; set; } = new List<ReviewerAssignment>();

    public virtual ICollection<Review> Reviews { get; set; } = new List<Review>();
}
