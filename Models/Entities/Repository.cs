using System;
using System.Collections.Generic;

namespace Models.Entities;

public partial class Repository
{
    public int Id { get; set; }

    public int OwnerId { get; set; }

    public long GitHubRepoId { get; set; }

    public string Name { get; set; } = null!;

    public string FullName { get; set; } = null!;

    public string? Description { get; set; }

    public string? Language { get; set; }

    public string DefaultBranch { get; set; } = null!;

    public bool IsPrivate { get; set; }

    public bool IsActive { get; set; }

    public string? GitHubUrl { get; set; }

    public long? WebhookId { get; set; }

    public string? WebhookSecret { get; set; }

    public bool WebhookActive { get; set; }

    public bool AutoReview { get; set; }

    public string SeverityThreshold { get; set; } = null!;

    public int MaxFilesPerReview { get; set; }

    public bool NotifyOnHigh { get; set; }

    public DateTime? LastSyncedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public int? OrganizationId { get; set; }

    public string? CustomInstructions { get; set; }

    public bool UseRag { get; set; }

    public virtual ICollection<ActivityLog> ActivityLogs { get; set; } = new List<ActivityLog>();

    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();

    public virtual Organization? Organization { get; set; }

    public virtual User Owner { get; set; } = null!;

    public virtual ICollection<PullRequest> PullRequests { get; set; } = new List<PullRequest>();

    public virtual RepoIndexState? RepoIndexState { get; set; }

    public virtual ICollection<RepoIndexedFile> RepoIndexedFiles { get; set; } = new List<RepoIndexedFile>();

    public virtual ICollection<RepositoryReviewer> RepositoryReviewers { get; set; } = new List<RepositoryReviewer>();

    public virtual ICollection<ReviewRule> ReviewRules { get; set; } = new List<ReviewRule>();
}
