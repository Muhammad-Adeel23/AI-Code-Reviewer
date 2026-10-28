using Microsoft.EntityFrameworkCore;
using Models.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Models.DTOs.PullRequestDTO;

namespace Repository.PullRequest
{
    public class PullRequestRepository : IPullRequestRepository
    {
        private readonly AppDbContext _db;

        public PullRequestRepository(AppDbContext db)
        {
            _db = db;
        }

        // ─── All PRs (all repos of organization) ─────────────────────────────────

        public async Task<PagedResult<PullRequestListDto>> GetAllAsync(
            int organizationId, int userId, PrFilterRequest filter, string orgRole)
        {
            var query = _db.PullRequests
                .Include(pr => pr.Repository)
                .Include(pr => pr.AuthorUser)
                .Where(pr => pr.Repository.OrganizationId == organizationId
                          && pr.Repository.IsActive);

            if (string.Equals(orgRole, "Developer", StringComparison.OrdinalIgnoreCase))
            {
                var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
                var userEmail = user?.Email?.Trim().ToLower() ?? "";
                var gitHubLogin = user?.GitHubLogin?.Trim().ToLower() ?? "";

                query = query.Where(pr =>
                    pr.AuthorUserId == userId ||
                    (pr.AuthorUser != null && pr.AuthorUser.Email.ToLower() == userEmail) ||
                    (!string.IsNullOrEmpty(gitHubLogin) && pr.AuthorGitHubLogin.ToLower() == gitHubLogin));
            }

            query = ApplyFilters(query, filter);

            var total = await query.CountAsync();

            var data = await query
                .OrderByDescending(pr => pr.GitHubUpdatedAt)
                .Skip((filter.Page - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .Select(pr => MapToListDto(pr))
                .ToListAsync();

            return new PagedResult<PullRequestListDto>
            {
                Data = data,
                Total = total,
                Page = filter.Page,
                PageSize = filter.PageSize
            };
        }

        // ─── Repo ki PRs ─────────────────────────────────────────────────────────

        public async Task<PagedResult<PullRequestListDto>> GetByRepoAsync(
            int repoId, int userId, PrFilterRequest filter, string orgRole)
        {
            var query = _db.PullRequests
                .Include(pr => pr.Repository)
                .Include(pr => pr.AuthorUser)
                .Where(pr => pr.RepositoryId == repoId);

            if (string.Equals(orgRole, "Developer", StringComparison.OrdinalIgnoreCase))
            {
                var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
                var userEmail = user?.Email?.Trim().ToLower() ?? "";
                var gitHubLogin = user?.GitHubLogin?.Trim().ToLower() ?? "";

                query = query.Where(pr =>
                    pr.AuthorUserId == userId ||
                    (pr.AuthorUser != null && pr.AuthorUser.Email.ToLower() == userEmail) ||
                    (!string.IsNullOrEmpty(gitHubLogin) && pr.AuthorGitHubLogin.ToLower() == gitHubLogin));
            }

            query = ApplyFilters(query, filter);

            var total = await query.CountAsync();

            var data = await query
                .OrderByDescending(pr => pr.GitHubUpdatedAt)
                .Skip((filter.Page - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .Select(pr => MapToListDto(pr))
                .ToListAsync();

            return new PagedResult<PullRequestListDto>
            {
                Data = data,
                Total = total,
                Page = filter.Page,
                PageSize = filter.PageSize
            };
        }

        // ─── PR Detail ────────────────────────────────────────────────────────────

        public async Task<PullRequestDetailDto?> GetByIdAsync(int prId)
        {
            var pr = await _db.PullRequests
                .Include(p => p.Repository)
                .Include(p => p.PullRequestFiles)
                .Include(p => p.Reviews.OrderByDescending(r => r.Version).Take(1))
                .FirstOrDefaultAsync(p => p.Id == prId);

            if (pr is null) return null;

            var latestReview = pr.Reviews.FirstOrDefault();

            return new PullRequestDetailDto
            {
                Id = pr.Id,
                Number = pr.Number,
                Title = pr.Title,
                Description = pr.Description,
                AuthorGitHubLogin = pr.AuthorGitHubLogin,
                AuthorAvatarUrl = pr.AuthorAvatarUrl,
                SourceBranch = pr.SourceBranch,
                TargetBranch = pr.TargetBranch,
                Status = pr.Status,
                ReviewStatus = pr.ReviewStatus,
                MergeRecommendation = pr.MergeRecommendation,
                CurrentReviewVersion = pr.CurrentReviewVersion,
                HighIssueCount = pr.HighIssueCount,
                MediumIssueCount = pr.MediumIssueCount,
                LowIssueCount = pr.LowIssueCount,
                LatestAiScore = pr.LatestAiScore,
                TotalAdditions = pr.TotalAdditions,
                TotalDeletions = pr.TotalDeletions,
                ChangedFilesCount = pr.ChangedFilesCount,
                GitHubPrUrl = pr.GitHubPrUrl,
                GitHubCreatedAt = pr.GitHubCreatedAt,
                MergedAt = pr.MergedAt,
                Repository = new RepoInfoDto
                {
                    Id = pr.Repository.Id,
                    Name = pr.Repository.Name,
                    FullName = pr.Repository.FullName
                },
                ChangedFiles = pr.PullRequestFiles.Select(f => new PullRequestFileDto
                {
                    FileName = f.FileName,
                    Status = f.Status,
                    Additions = f.Additions,
                    Deletions = f.Deletions,
                    Patch = f.Patch
                }).ToList(),
                LatestReview = latestReview is null ? null : new ReviewSummaryDto
                {
                    Id = latestReview.Id,
                    Version = latestReview.Version,
                    CodeQualityScore = latestReview.CodeQualityScore,
                    SecurityScore = latestReview.SecurityScore,
                    MaintainabilityScore = latestReview.MaintainabilityScore,
                    OverallScore = latestReview.OverallScore,
                    MergeRecommendation = latestReview.MergeRecommendation,
                    AiSummary = latestReview.AiSummary,
                    HighIssueCount = latestReview.HighIssueCount,
                    MediumIssueCount = latestReview.MediumIssueCount,
                    LowIssueCount = latestReview.LowIssueCount,
                    ReviewedAt = latestReview.ReviewedAt
                }
            };
        }

        public async Task<Models.Entities.PullRequest?> GetEntityByIdAsync(int prId)
        {
            return await _db.PullRequests
                .Include(p => p.Repository)
                .FirstOrDefaultAsync(p => p.Id == prId);
        }

        public async Task UpdateReviewStatusAsync(int prId, string reviewStatus)
        {
            var pr = await _db.PullRequests.FindAsync(prId);
            if (pr is null) return;
            pr.ReviewStatus = reviewStatus;
            pr.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        public async Task<bool> IsRepoOwnedByOrgAsync(int repoId, int organizationId)
        {
            return await _db.Repositories
                .AnyAsync(r => r.Id == repoId
                            && r.OrganizationId == organizationId
                            && r.IsActive);
        }

        public async Task<bool> IsPrInOrgAsync(int prId, int organizationId)
        {
            return await _db.PullRequests
                .AnyAsync(pr => pr.Id == prId
                             && pr.Repository.OrganizationId == organizationId);
        }

        // ─── Private Helpers ──────────────────────────────────────────────────────

        private static IQueryable<Models.Entities.PullRequest> ApplyFilters(
            IQueryable<Models.Entities.PullRequest> query, PrFilterRequest filter)
        {
            // Status filter
            if (!string.IsNullOrEmpty(filter.Status) && filter.Status != "All")
                query = query.Where(pr => pr.Status == filter.Status);

            // Review status filter
            if (!string.IsNullOrEmpty(filter.ReviewStatus) && filter.ReviewStatus != "All")
                query = query.Where(pr => pr.ReviewStatus == filter.ReviewStatus);

            // Search
            if (!string.IsNullOrEmpty(filter.Search))
                query = query.Where(pr =>
                    pr.Title.Contains(filter.Search) ||
                    pr.AuthorGitHubLogin.Contains(filter.Search) ||
                    pr.SourceBranch.Contains(filter.Search));

            return query;
        }

        private static PullRequestListDto MapToListDto(Models.Entities.PullRequest pr) =>
            new()
            {
                Id = pr.Id,
                Number = pr.Number,
                Title = pr.Title,
                AuthorGitHubLogin = pr.AuthorGitHubLogin,
                AuthorAvatarUrl = pr.AuthorAvatarUrl,
                SourceBranch = pr.SourceBranch,
                TargetBranch = pr.TargetBranch,
                Status = pr.Status,
                ReviewStatus = pr.ReviewStatus,
                HighIssueCount = pr.HighIssueCount,
                MediumIssueCount = pr.MediumIssueCount,
                LowIssueCount = pr.LowIssueCount,
                LatestAiScore = pr.LatestAiScore,
                MergeRecommendation = pr.MergeRecommendation,
                CurrentReviewVersion = pr.CurrentReviewVersion,
                GitHubPrUrl = pr.GitHubPrUrl,
                RepoName = pr.Repository.Name,
                RepoId = pr.Repository.Id,
                GitHubCreatedAt = pr.GitHubCreatedAt,
                GitHubUpdatedAt = pr.GitHubUpdatedAt
            };
    }
}