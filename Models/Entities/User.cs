using System;
using System.Collections.Generic;

namespace Models.Entities;

public partial class User
{
    public int Id { get; set; }

    public string FullName { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string? PasswordHash { get; set; }

    public string? AvatarUrl { get; set; }

    public string? GitHubLogin { get; set; }

    public long? GitHubId { get; set; }

    public string? GitHubAccessToken { get; set; }

    public string Role { get; set; } = null!;

    public bool IsActive { get; set; }

    public bool EmailVerified { get; set; }

    public DateTime? LastLoginAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual ICollection<ActivityLog> ActivityLogs { get; set; } = new List<ActivityLog>();

    public virtual ICollection<McpApiKey> McpApiKeys { get; set; } = new List<McpApiKey>();

    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();

    public virtual ICollection<OauthState> OauthStates { get; set; } = new List<OauthState>();

    public virtual ICollection<OrganizationMember> OrganizationMembers { get; set; } = new List<OrganizationMember>();

    public virtual ICollection<Organization> Organizations { get; set; } = new List<Organization>();

    public virtual ICollection<PasswordResetToken> PasswordResetTokens { get; set; } = new List<PasswordResetToken>();

    public virtual ICollection<PullRequest> PullRequests { get; set; } = new List<PullRequest>();

    public virtual ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();

    public virtual ICollection<Repository> Repositories { get; set; } = new List<Repository>();

    public virtual ICollection<RepositoryReviewer> RepositoryReviewerAssignedByNavigations { get; set; } = new List<RepositoryReviewer>();

    public virtual ICollection<RepositoryReviewer> RepositoryReviewerReviewers { get; set; } = new List<RepositoryReviewer>();

    public virtual ICollection<ReviewIssue> ReviewIssues { get; set; } = new List<ReviewIssue>();

    public virtual ICollection<ReviewerAssignment> ReviewerAssignmentAssignedByNavigations { get; set; } = new List<ReviewerAssignment>();

    public virtual ICollection<ReviewerAssignment> ReviewerAssignmentReviewers { get; set; } = new List<ReviewerAssignment>();

    public virtual ICollection<TeamInvitation> TeamInvitations { get; set; } = new List<TeamInvitation>();

    public virtual UserSetting? UserSetting { get; set; }
}
