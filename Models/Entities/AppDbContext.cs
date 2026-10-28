using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace Models.Entities;

public partial class AppDbContext : DbContext
{
    public AppDbContext()
    {
    }

    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<ActivityLog> ActivityLogs { get; set; }

    public virtual DbSet<ApplicationLookup> ApplicationLookups { get; set; }

    public virtual DbSet<ApplicationLookupType> ApplicationLookupTypes { get; set; }

    public virtual DbSet<EmailNotification> EmailNotifications { get; set; }

    public virtual DbSet<InvitationTemplate> InvitationTemplates { get; set; }

    public virtual DbSet<McpApiKey> McpApiKeys { get; set; }

    public virtual DbSet<Notification> Notifications { get; set; }

    public virtual DbSet<OauthState> OauthStates { get; set; }

    public virtual DbSet<Organization> Organizations { get; set; }

    public virtual DbSet<OrganizationMember> OrganizationMembers { get; set; }

    public virtual DbSet<PasswordResetToken> PasswordResetTokens { get; set; }

    public virtual DbSet<PullRequest> PullRequests { get; set; }

    public virtual DbSet<PullRequestFile> PullRequestFiles { get; set; }

    public virtual DbSet<RefreshToken> RefreshTokens { get; set; }

    public virtual DbSet<Repository> Repositories { get; set; }

    public virtual DbSet<RepositoryReviewer> RepositoryReviewers { get; set; }

    public virtual DbSet<Review> Reviews { get; set; }

    public virtual DbSet<ReviewIssue> ReviewIssues { get; set; }

    public virtual DbSet<ReviewerAssignment> ReviewerAssignments { get; set; }

    public virtual DbSet<Setting> Settings { get; set; }

    public virtual DbSet<TeamInvitation> TeamInvitations { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<UserSetting> UserSettings { get; set; }


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ActivityLog>(entity =>
        {
            entity.HasIndex(e => new { e.RepositoryId, e.CreatedAt }, "IX_AL_RepoId").IsDescending(false, true);

            entity.HasIndex(e => new { e.Type, e.CreatedAt }, "IX_AL_Type").IsDescending(false, true);

            entity.HasIndex(e => new { e.UserId, e.CreatedAt }, "IX_AL_UserId").IsDescending(false, true);

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getutcdate())");
            entity.Property(e => e.IpAddress).HasMaxLength(50);
            entity.Property(e => e.Message).HasMaxLength(500);
            entity.Property(e => e.Status)
                .HasMaxLength(10)
                .HasDefaultValue("Success");
            entity.Property(e => e.Type).HasMaxLength(50);

            entity.HasOne(d => d.PullRequest).WithMany(p => p.ActivityLogs)
                .HasForeignKey(d => d.PullRequestId)
                .HasConstraintName("FK_AL_PR");

            entity.HasOne(d => d.Repository).WithMany(p => p.ActivityLogs)
                .HasForeignKey(d => d.RepositoryId)
                .HasConstraintName("FK_AL_Repo");

            entity.HasOne(d => d.Review).WithMany(p => p.ActivityLogs)
                .HasForeignKey(d => d.ReviewId)
                .HasConstraintName("FK_AL_Review");

            entity.HasOne(d => d.User).WithMany(p => p.ActivityLogs)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK_AL_User");
        });

        modelBuilder.Entity<ApplicationLookup>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Applicat__3214EC0765E4ED45");

            entity.ToTable("ApplicationLookup");

            entity.HasIndex(e => e.SeqKey, "UQ_ApplicationLookup_SeqKey").IsUnique();

            entity.Property(e => e.ColorCode).HasMaxLength(250);
            entity.Property(e => e.Description).HasMaxLength(250);
            entity.Property(e => e.Icon).HasMaxLength(250);
            entity.Property(e => e.Name).HasMaxLength(250);
            entity.Property(e => e.SeqKey).IsRequired();

            entity.HasOne(d => d.Type).WithMany(p => p.ApplicationLookups)
                .HasForeignKey(d => d.TypeId)
                .HasConstraintName("FK__Applicati__TypeI__6FB49575");
        });

        modelBuilder.Entity<ApplicationLookupType>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Applicat__3214EC07617EFDC1");

            entity.ToTable("ApplicationLookupType");

            entity.Property(e => e.Name).HasMaxLength(250);
        });

        modelBuilder.Entity<EmailNotification>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__EmailNot__3214EC07E2F47749");

            entity.Property(e => e.Bcc)
                .HasMaxLength(500)
                .HasColumnName("BCC");
            entity.Property(e => e.Cc)
                .HasMaxLength(500)
                .HasColumnName("CC");
            entity.Property(e => e.From).HasMaxLength(255);
            entity.Property(e => e.Subject).HasMaxLength(500);
            entity.Property(e => e.To).HasMaxLength(255);

            entity.HasOne(d => d.Status).WithMany(p => p.EmailNotificationStatuses)
                .HasPrincipalKey(p => p.SeqKey)
                .HasForeignKey(d => d.StatusId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_EmailNotification_Status");

            entity.HasOne(d => d.TemplateSeqKeyNavigation).WithMany(p => p.EmailNotificationTemplateSeqKeyNavigations)
                .HasPrincipalKey(p => p.SeqKey)
                .HasForeignKey(d => d.TemplateSeqKey)
                .HasConstraintName("FK_EmailNotification_Template");
        });

        modelBuilder.Entity<InvitationTemplate>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Invitati__3214EC07391A8106");

            entity.ToTable("InvitationTemplate");

            entity.Property(e => e.Subject).HasMaxLength(500);
            entity.Property(e => e.TemplateName).HasMaxLength(255);

            entity.HasOne(d => d.SeqKeyNavigation).WithMany(p => p.InvitationTemplates)
                .HasPrincipalKey(p => p.SeqKey)
                .HasForeignKey(d => d.SeqKey)
                .HasConstraintName("FK_InvitationTemplate_ApplicationLookup");
        });

        modelBuilder.Entity<McpApiKey>(entity =>
        {
            entity.HasIndex(e => e.KeyHash, "IX_MAK_Hash");

            entity.HasIndex(e => e.UserId, "IX_MAK_UserId");

            entity.HasIndex(e => e.KeyHash, "UQ_MAK_KeyHash").IsUnique();

            entity.HasIndex(e => e.KeyPrefix, "UQ_MAK_KeyPrefix").IsUnique();

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getutcdate())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.KeyHash).HasMaxLength(512);
            entity.Property(e => e.KeyPrefix).HasMaxLength(20);
            entity.Property(e => e.LastUsedIp).HasMaxLength(50);
            entity.Property(e => e.Name).HasMaxLength(100);

            entity.HasOne(d => d.User).WithMany(p => p.McpApiKeys)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK_MAK_User");
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasIndex(e => new { e.UserId, e.CreatedAt }, "IX_Notif_Created").IsDescending(false, true);

            entity.HasIndex(e => new { e.UserId, e.IsRead }, "IX_Notif_UserId");

            entity.Property(e => e.ActionUrl).HasMaxLength(300);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getutcdate())");
            entity.Property(e => e.Message).HasMaxLength(500);
            entity.Property(e => e.Severity).HasMaxLength(10);
            entity.Property(e => e.Title).HasMaxLength(200);
            entity.Property(e => e.Type).HasMaxLength(50);

            entity.HasOne(d => d.PullRequest).WithMany(p => p.Notifications)
                .HasForeignKey(d => d.PullRequestId)
                .HasConstraintName("FK_Notif_PR");

            entity.HasOne(d => d.Repository).WithMany(p => p.Notifications)
                .HasForeignKey(d => d.RepositoryId)
                .HasConstraintName("FK_Notif_Repo");

            entity.HasOne(d => d.Review).WithMany(p => p.Notifications)
                .HasForeignKey(d => d.ReviewId)
                .HasConstraintName("FK_Notif_Review");

            entity.HasOne(d => d.User).WithMany(p => p.Notifications)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK_Notif_User");
        });

        modelBuilder.Entity<OauthState>(entity =>
        {
            entity.ToTable("OAuthStates");

            entity.HasIndex(e => e.State, "UQ_OAuthStates_State").IsUnique();

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getutcdate())");
            entity.Property(e => e.Provider).HasMaxLength(20);
            entity.Property(e => e.State).HasMaxLength(100);

            entity.HasOne(d => d.User).WithMany(p => p.OauthStates)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_OAuthStates_User");
        });

        modelBuilder.Entity<Organization>(entity =>
        {
            entity.HasIndex(e => e.Slug, "UQ_Organizations_Slug").IsUnique();

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getutcdate())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Name).HasMaxLength(150);
            entity.Property(e => e.Slug).HasMaxLength(100);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getutcdate())");

            entity.HasOne(d => d.Owner).WithMany(p => p.Organizations)
                .HasForeignKey(d => d.OwnerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Org_Owner");
        });

        modelBuilder.Entity<OrganizationMember>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_OrgMembers");

            entity.HasIndex(e => new { e.OrganizationId, e.UserId }, "UQ_OM_OrgUser").IsUnique();

            entity.Property(e => e.InvitedAt).HasDefaultValueSql("(getutcdate())");
            entity.Property(e => e.Role)
                .HasMaxLength(20)
                .HasDefaultValue("Developer");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValue("Active");

            entity.HasOne(d => d.Organization).WithMany(p => p.OrganizationMembers)
                .HasForeignKey(d => d.OrganizationId)
                .HasConstraintName("FK_OM_Org");

            entity.HasOne(d => d.User).WithMany(p => p.OrganizationMembers)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_OM_User");
        });

        modelBuilder.Entity<PasswordResetToken>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Password__3214EC075494F58E");

            entity.ToTable("PasswordResetToken");

            entity.HasIndex(e => e.ExpiresAt, "IX_PasswordResetToken_ExpiresAt");

            entity.HasIndex(e => e.TokenHash, "IX_PasswordResetToken_TokenHash");

            entity.HasIndex(e => e.UserId, "IX_PasswordResetToken_UserId");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getutcdate())");
            entity.Property(e => e.TokenHash).HasMaxLength(256);

            entity.HasOne(d => d.User).WithMany(p => p.PasswordResetTokens)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK_PasswordResetToken_User");
        });

        modelBuilder.Entity<PullRequest>(entity =>
        {
            entity.HasIndex(e => new { e.RepositoryId, e.Number }, "IX_PR_Number");

            entity.HasIndex(e => e.RepositoryId, "IX_PR_RepoId");

            entity.HasIndex(e => e.ReviewStatus, "IX_PR_ReviewStatus");

            entity.HasIndex(e => new { e.RepositoryId, e.Status }, "IX_PR_Status");

            entity.HasIndex(e => e.GitHubPrId, "UQ_PR_GitHubId").IsUnique();

            entity.Property(e => e.AuthorAvatarUrl).HasMaxLength(500);
            entity.Property(e => e.AuthorGitHubLogin).HasMaxLength(100);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getutcdate())");
            entity.Property(e => e.GitHubPrUrl).HasMaxLength(500);
            entity.Property(e => e.ReviewStatus)
                .HasMaxLength(20)
                .HasDefaultValue("Pending");
            entity.Property(e => e.SourceBranch).HasMaxLength(200);
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValue("Open");
            entity.Property(e => e.TargetBranch).HasMaxLength(200);
            entity.Property(e => e.Title).HasMaxLength(500);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getutcdate())");

            entity.HasOne(d => d.AuthorUser).WithMany(p => p.PullRequests)
                .HasForeignKey(d => d.AuthorUserId)
                .HasConstraintName("FK_PR_Author");

            entity.HasOne(d => d.Repository).WithMany(p => p.PullRequests)
                .HasForeignKey(d => d.RepositoryId)
                .HasConstraintName("FK_PR_Repo");
        });

        modelBuilder.Entity<PullRequestFile>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_PRFiles");

            entity.HasIndex(e => e.PullRequestId, "IX_PRFiles_PRId");

            entity.Property(e => e.FileName).HasMaxLength(500);
            entity.Property(e => e.Status).HasMaxLength(20);

            entity.HasOne(d => d.PullRequest).WithMany(p => p.PullRequestFiles)
                .HasForeignKey(d => d.PullRequestId)
                .HasConstraintName("FK_PRFiles_PR");
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.HasIndex(e => e.Token, "IX_RefreshTokens_Token");

            entity.HasIndex(e => e.UserId, "IX_RefreshTokens_UserId");

            entity.HasIndex(e => e.Token, "UQ_RefreshTokens_Token").IsUnique();

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getutcdate())");
            entity.Property(e => e.CreatedByIp).HasMaxLength(50);
            entity.Property(e => e.Token).HasMaxLength(512);

            entity.HasOne(d => d.User).WithMany(p => p.RefreshTokens)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK_RefreshTokens_Users");
        });

        modelBuilder.Entity<Repository>(entity =>
        {
            entity.HasIndex(e => e.FullName, "IX_Repo_FullName");

            entity.HasIndex(e => e.OwnerId, "IX_Repo_OwnerId");

            entity.HasIndex(e => e.GitHubRepoId, "UQ_Repo_GitHubId").IsUnique();

            entity.Property(e => e.AutoReview).HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getutcdate())");
            entity.Property(e => e.DefaultBranch)
                .HasMaxLength(100)
                .HasDefaultValue("main");
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.FullName).HasMaxLength(300);
            entity.Property(e => e.GitHubUrl).HasMaxLength(500);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Language).HasMaxLength(50);
            entity.Property(e => e.MaxFilesPerReview).HasDefaultValue(20);
            entity.Property(e => e.Name).HasMaxLength(200);
            entity.Property(e => e.NotifyOnHigh).HasDefaultValue(true);
            entity.Property(e => e.SeverityThreshold)
                .HasMaxLength(20)
                .HasDefaultValue("ALL");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getutcdate())");
            entity.Property(e => e.WebhookSecret).HasMaxLength(256);

            entity.HasOne(d => d.Organization).WithMany(p => p.Repositories)
                .HasForeignKey(d => d.OrganizationId)
                .HasConstraintName("FK_Repo_Org");

            entity.HasOne(d => d.Owner).WithMany(p => p.Repositories)
                .HasForeignKey(d => d.OwnerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Repo_Owner");
        });

        modelBuilder.Entity<RepositoryReviewer>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_RepoReviewers");

            entity.HasIndex(e => new { e.RepositoryId, e.ReviewerId }, "UQ_RR_RepoReviewer").IsUnique();

            entity.Property(e => e.AssignedAt).HasDefaultValueSql("(getutcdate())");

            entity.HasOne(d => d.AssignedByNavigation).WithMany(p => p.RepositoryReviewerAssignedByNavigations)
                .HasForeignKey(d => d.AssignedBy)
                .HasConstraintName("FK_RR_AssignedBy");

            entity.HasOne(d => d.Repository).WithMany(p => p.RepositoryReviewers)
                .HasForeignKey(d => d.RepositoryId)
                .HasConstraintName("FK_RR_Repo");

            entity.HasOne(d => d.Reviewer).WithMany(p => p.RepositoryReviewerReviewers)
                .HasForeignKey(d => d.ReviewerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_RR_Reviewer");
        });

        modelBuilder.Entity<Review>(entity =>
        {
            entity.HasIndex(e => e.PullRequestId, "IX_Rev_PRId");

            entity.HasIndex(e => e.Status, "IX_Rev_Status");

            entity.HasIndex(e => new { e.PullRequestId, e.Version }, "UQ_Rev_PRVersion").IsUnique();

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getutcdate())");
            entity.Property(e => e.ErrorMessage).HasMaxLength(500);
            entity.Property(e => e.HangfireJobId).HasMaxLength(100);
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValue("Pending");
            entity.Property(e => e.Version).HasDefaultValue(1);

            entity.HasOne(d => d.PullRequest).WithMany(p => p.Reviews)
                .HasForeignKey(d => d.PullRequestId)
                .HasConstraintName("FK_Rev_PR");
        });

        modelBuilder.Entity<ReviewIssue>(entity =>
        {
            entity.HasIndex(e => e.ReviewId, "IX_RI_ReviewId");

            entity.HasIndex(e => new { e.ReviewId, e.Severity }, "IX_RI_Severity");

            entity.Property(e => e.Category).HasMaxLength(50);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getutcdate())");
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.FileName).HasMaxLength(500);
            entity.Property(e => e.Severity).HasMaxLength(10);
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValue("Open");
            entity.Property(e => e.Suggestion).HasMaxLength(1000);

            entity.HasOne(d => d.Review).WithMany(p => p.ReviewIssues)
                .HasForeignKey(d => d.ReviewId)
                .HasConstraintName("FK_RI_Review");

            entity.HasOne(d => d.StatusUpdatedByNavigation).WithMany(p => p.ReviewIssues)
                .HasForeignKey(d => d.StatusUpdatedBy)
                .HasConstraintName("FK_RI_StatusUser");
        });

        modelBuilder.Entity<ReviewerAssignment>(entity =>
        {
            entity.HasIndex(e => e.PullRequestId, "IX_RA_PullRequestId");

            entity.HasIndex(e => e.ReviewerId, "IX_RA_ReviewerId");

            entity.Property(e => e.AssignedAt).HasDefaultValueSql("(getutcdate())");
            entity.Property(e => e.Message).HasMaxLength(500);
            entity.Property(e => e.ReviewerComment).HasMaxLength(1000);
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValue("Pending");

            entity.HasOne(d => d.AssignedByNavigation).WithMany(p => p.ReviewerAssignmentAssignedByNavigations)
                .HasForeignKey(d => d.AssignedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_RA_AssignedBy");

            entity.HasOne(d => d.PullRequest).WithMany(p => p.ReviewerAssignments)
                .HasForeignKey(d => d.PullRequestId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_RA_PR");

            entity.HasOne(d => d.Review).WithMany(p => p.ReviewerAssignments)
                .HasForeignKey(d => d.ReviewId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_RA_Review");

            entity.HasOne(d => d.Reviewer).WithMany(p => p.ReviewerAssignmentReviewers)
                .HasForeignKey(d => d.ReviewerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_RA_Reviewer");
        });

        modelBuilder.Entity<Setting>(entity =>
        {
            entity.ToTable("Setting");

            entity.Property(e => e.Key)
                .HasMaxLength(250)
                .IsUnicode(false);
            entity.Property(e => e.Value).IsUnicode(false);
        });

        modelBuilder.Entity<TeamInvitation>(entity =>
        {
            entity.HasIndex(e => e.Token, "UQ_TI_Token").IsUnique();

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getutcdate())");
            entity.Property(e => e.Email).HasMaxLength(256);
            entity.Property(e => e.Role)
                .HasMaxLength(20)
                .HasDefaultValue("Developer");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValue("Pending");
            entity.Property(e => e.Token).HasMaxLength(256);

            entity.HasOne(d => d.InvitedByNavigation).WithMany(p => p.TeamInvitations)
                .HasForeignKey(d => d.InvitedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TI_InvitedBy");

            entity.HasOne(d => d.Organization).WithMany(p => p.TeamInvitations)
                .HasForeignKey(d => d.OrganizationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TeamInvitations_Organizations");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(e => e.Email, "IX_Users_Email");

            entity.HasIndex(e => e.GitHubLogin, "IX_Users_GitHubLogin");

            entity.HasIndex(e => e.Email, "UQ_Users_Email").IsUnique();

            entity.Property(e => e.AvatarUrl).HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getutcdate())");
            entity.Property(e => e.Email).HasMaxLength(256);
            entity.Property(e => e.FullName).HasMaxLength(150);
            entity.Property(e => e.GitHubAccessToken).HasMaxLength(512);
            entity.Property(e => e.GitHubLogin).HasMaxLength(100);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.PasswordHash).HasMaxLength(512);
            entity.Property(e => e.Role)
                .HasMaxLength(20)
                .HasDefaultValue("Developer");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getutcdate())");
        });

        modelBuilder.Entity<UserSetting>(entity =>
        {
            entity.HasIndex(e => e.UserId, "UQ_US_UserId").IsUnique();

            entity.Property(e => e.AutoReviewOnPrOpen).HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getutcdate())");
            entity.Property(e => e.EmailOnHighSeverity).HasDefaultValue(true);
            entity.Property(e => e.EmailOnReviewComplete).HasDefaultValue(true);
            entity.Property(e => e.InAppNotifications).HasDefaultValue(true);
            entity.Property(e => e.MaxFilesPerReview).HasDefaultValue(20);
            entity.Property(e => e.SeverityThreshold)
                .HasMaxLength(20)
                .HasDefaultValue("ALL");
            entity.Property(e => e.Theme)
                .HasMaxLength(10)
                .HasDefaultValue("dark");
            entity.Property(e => e.Timezone)
                .HasMaxLength(50)
                .HasDefaultValue("UTC");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getutcdate())");

            entity.HasOne(d => d.User).WithOne(p => p.UserSetting)
                .HasForeignKey<UserSetting>(d => d.UserId)
                .HasConstraintName("FK_US_User");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
