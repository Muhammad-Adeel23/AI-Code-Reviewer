using Models.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Services.Rule
{
    public interface IReviewRuleService
    {
        Task<List<RuleDto>> ListAsync(int organizationId, int? repositoryId);
        Task<RuleDto> CreateAsync(int organizationId, int userId, UpsertRuleRequest request);
        Task<RuleDto> UpdateAsync(int organizationId, int ruleId, UpsertRuleRequest request);
        Task DeleteAsync(int organizationId, int ruleId);
        IReadOnlyList<RuleTemplateDto> GetTemplates();
    }
}
