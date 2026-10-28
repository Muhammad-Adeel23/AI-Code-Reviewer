using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Models.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Models.DTOs.GitHubDTO;

namespace Repository.GitHubRepos
{
    public class GitHubRepositoryRepository : IGitHubRepositoryRepository
    {
        private readonly AppDbContext _db;
        private readonly ILogger<GitHubRepositoryRepository> _logger;

        public GitHubRepositoryRepository(AppDbContext db, ILogger<GitHubRepositoryRepository> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task<bool> IsAlreadyImportedAsync(long githubRepoId)
        {
            return await _db.Repositories.AnyAsync(r => r.GitHubRepoId == githubRepoId);
        }

        public async Task<int> SaveRepositoryAsync(Models.Entities.Repository repo)
        {
            _db.Repositories.Add(repo);
            await _db.SaveChangesAsync();
            return repo.Id;
        }

        public async Task<List<RepositoryDto>> GetUserRepositoriesAsync(int userId)
        {
            var orgId = await _db.OrganizationMembers
                .Where(m => m.UserId == userId && m.Status == "Active")
                .Select(m => (int?)m.OrganizationId)
                .FirstOrDefaultAsync();

            return await _db.Repositories
                .Where(r => (r.OwnerId == userId || (orgId != null && r.OrganizationId == orgId)) && r.IsActive)
                .Select(r => new RepositoryDto
                {
                    Id = r.Id,
                    Name = r.Name,
                    FullName = r.FullName,
                    Language = r.Language,
                    IsActive = r.IsActive,
                    WebhookActive = r.WebhookActive,
                    LastSyncedAt = r.LastSyncedAt,
                    OpenPrCount = r.PullRequests.Count(p => p.Status == "Open")
                })
                .OrderByDescending(r => r.Id)
                .ToListAsync();
        }

        public async Task<Models.Entities.Repository?> GetByIdAsync(int repoId, int userId)
        {
            var orgId = await _db.OrganizationMembers
                .Where(m => m.UserId == userId && m.Status == "Active")
                .Select(m => (int?)m.OrganizationId)
                .FirstOrDefaultAsync();

            return await _db.Repositories
                .FirstOrDefaultAsync(r => r.Id == repoId && (r.OwnerId == userId || (orgId != null && r.OrganizationId == orgId)) && r.IsActive);
        }

        public async Task<string?> GetOwnerGitHubTokenAsync(int repoId)
        {
            return await _db.Repositories
                .Where(r => r.Id == repoId)
                .Select(r => r.Owner.GitHubAccessToken)
                .FirstOrDefaultAsync();
        }

        public async Task UpdateLastSyncedAsync(int repoId)
        {
            var repo = await _db.Repositories.FindAsync(repoId);
            if (repo is null) return;
            repo.LastSyncedAt = DateTime.UtcNow;
            repo.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        public async Task SavePullRequestAsync(Models.Entities.PullRequest pr)
        {
            _db.PullRequests.Add(pr);
            await _db.SaveChangesAsync();
        }

        public async Task<bool> PrExistsAsync(long githubPrId)
        {
            return await _db.PullRequests.AnyAsync(p => p.GitHubPrId == githubPrId);
        }

        public async Task UpdatePrStatusAsync(long githubPrId, string status)
        {
            var pr = await _db.PullRequests
                .FirstOrDefaultAsync(p => p.GitHubPrId == githubPrId);
            if (pr is null) return;
            pr.Status = status;
            pr.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }
    }
}