using Models.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Repository.Rules
{
    public interface IReviewRuleRepository
    {
        /// <summary>For the UI: org-wide rules + (if repositoryId given) that repo's rules; otherwise every rule of the org.</summary>
        Task<List<ReviewRule>> ListAsync(int organizationId, int? repositoryId);

        /// <summary>For the review pipeline: enabled org-wide rules + enabled rules of this repository.</summary>
        Task<List<ReviewRule>> GetActiveForRepositoryAsync(int organizationId, int repositoryId);

        Task<ReviewRule?> GetAsync(int organizationId, int ruleId);
        Task<int> CountAsync(int organizationId);
        Task<bool> RepositoryBelongsToOrganizationAsync(int organizationId, int repositoryId);

        Task<ReviewRule> AddAsync(ReviewRule rule);
        Task UpdateAsync(ReviewRule rule);
        Task DeleteAsync(ReviewRule rule);
    }

}
