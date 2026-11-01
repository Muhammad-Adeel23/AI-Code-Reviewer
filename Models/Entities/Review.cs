using System;
using System.Collections.Generic;

namespace Models.Entities;

public partial class Review
{
    public int Id { get; set; }

    public int PullRequestId { get; set; }

    public int Version { get; set; }

    public int? CodeQualityScore { get; set; }

    public int? SecurityScore { get; set; }

    public int? MaintainabilityScore { get; set; }

    public int? OverallScore { get; set; }

    public int HighIssueCount { get; set; }

    public int MediumIssueCount { get; set; }

    public int LowIssueCount { get; set; }

    public int SuggestionCount { get; set; }

    public bool? MergeRecommendation { get; set; }

    public string? AiSummary { get; set; }

    public string Status { get; set; } = null!;

    public string? HangfireJobId { get; set; }

    public string? ErrorMessage { get; set; }

    public byte RetryCount { get; set; }

    public int? PromptTokensUsed { get; set; }

    public int? CompletionTokensUsed { get; set; }

    public int? ProcessingMs { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public long? GitHubReviewId { get; set; }

    public DateTime? GitHubPostedAt { get; set; }

    public bool UsedRag { get; set; }

    public int? ContextChunkCount { get; set; }

    public string? ContextSummary { get; set; }

    public virtual ICollection<ActivityLog> ActivityLogs { get; set; } = new List<ActivityLog>();

    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();

    public virtual PullRequest PullRequest { get; set; } = null!;

    public virtual ICollection<ReviewIssue> ReviewIssues { get; set; } = new List<ReviewIssue>();

    public virtual ICollection<ReviewerAssignment> ReviewerAssignments { get; set; } = new List<ReviewerAssignment>();
}
