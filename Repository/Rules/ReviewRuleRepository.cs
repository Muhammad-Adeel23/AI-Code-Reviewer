using Models.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Repository.Rules
{
    public class ReviewRuleRepository : IReviewRuleRepository
    {
        private readonly AppDbContext _db;
        public ReviewRuleRepository(AppDbContext db) => _db = db;

        public Task<List<ReviewRule>> ListAsync(int organizationId, int? repositoryId)
        {
            var q = _db.ReviewRules.AsNoTracking().Where(r => r.OrganizationId == organizationId);
            if (repositoryId.HasValue)
                q = q.Where(r => r.RepositoryId == null || r.RepositoryId == repositoryId.Value);
            return q.OrderBy(r => r.RepositoryId != null).ThenBy(r => r.Id).ToListAsync();
        }

        public Task<List<ReviewRule>> GetActiveForRepositoryAsync(int organizationId, int repositoryId) =>
            _db.ReviewRules.AsNoTracking()
                .Where(r => r.OrganizationId == organizationId && r.IsEnabled
                            && (r.RepositoryId == null || r.RepositoryId == repositoryId))
                .OrderBy(r => r.Severity == "HIGH" ? 0 : r.Severity == "MEDIUM" ? 1 : r.Severity == "LOW" ? 2 : 3)
                .ThenBy(r => r.Id)
                .ToListAsync();

        public Task<ReviewRule?> GetAsync(int organizationId, int ruleId) =>
            _db.ReviewRules.FirstOrDefaultAsync(r => r.Id == ruleId && r.OrganizationId == organizationId);

        public Task<int> CountAsync(int organizationId) =>
            _db.ReviewRules.CountAsync(r => r.OrganizationId == organizationId);

        public Task<bool> RepositoryBelongsToOrganizationAsync(int organizationId, int repositoryId) =>
            _db.Repositories.AnyAsync(r => r.Id == repositoryId && r.OrganizationId == organizationId && r.IsActive);

        public async Task<ReviewRule> AddAsync(ReviewRule rule)
        {
            _db.ReviewRules.Add(rule);
            await _db.SaveChangesAsync();
            return rule;
        }

        public async Task UpdateAsync(ReviewRule rule)
        {
            rule.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        public async Task DeleteAsync(ReviewRule rule)
        {
            _db.ReviewRules.Remove(rule);
            await _db.SaveChangesAsync();
        }
    }

}
