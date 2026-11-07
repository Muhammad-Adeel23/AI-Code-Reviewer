using Models.DTOs;
using Models.Entities;
using Repository.Rules;


namespace Services.Rule
{
    public class ReviewRuleService : IReviewRuleService
    {
        private const int MaxRulesPerOrganization = 40;
        private static readonly string[] Severities = { "HIGH", "MEDIUM", "LOW", "SUGGESTION" };

        private readonly IReviewRuleRepository _rules;
        public ReviewRuleService(IReviewRuleRepository rules) => _rules = rules;

        public async Task<List<RuleDto>> ListAsync(int organizationId, int? repositoryId) =>
            (await _rules.ListAsync(organizationId, repositoryId)).Select(Map).ToList();

        public IReadOnlyList<RuleTemplateDto> GetTemplates() => RuleTemplates.All;

        public async Task<RuleDto> CreateAsync(int organizationId, int userId, UpsertRuleRequest request)
        {
            await ValidateAsync(organizationId, request);

            if (await _rules.CountAsync(organizationId) >= MaxRulesPerOrganization)
                throw new InvalidOperationException($"Maximum {MaxRulesPerOrganization} rules allowed per organization");

            var rule = new ReviewRule
            {
                OrganizationId = organizationId,
                CreatedBy = userId > 0 ? userId : null
            };
            Apply(rule, request);
            return Map(await _rules.AddAsync(rule));
        }

        public async Task<RuleDto> UpdateAsync(int organizationId, int ruleId, UpsertRuleRequest request)
        {
            var rule = await _rules.GetAsync(organizationId, ruleId)
                       ?? throw new KeyNotFoundException("Rule nahi mili");
            await ValidateAsync(organizationId, request);

            Apply(rule, request);
            await _rules.UpdateAsync(rule);
            return Map(rule);
        }

        public async Task DeleteAsync(int organizationId, int ruleId)
        {
            var rule = await _rules.GetAsync(organizationId, ruleId)
                       ?? throw new KeyNotFoundException("Rule nahi mili");
            await _rules.DeleteAsync(rule);
        }

        private async Task ValidateAsync(int organizationId, UpsertRuleRequest r)
        {
            if (string.IsNullOrWhiteSpace(r.Title) || r.Title.Trim().Length > 200)
                throw new ArgumentException("Title 1-200 characters ka hona chahiye");
            if (string.IsNullOrWhiteSpace(r.Description) || r.Description.Trim().Length > 1000)
                throw new ArgumentException("Description 1-1000 characters ki honi chahiye");
            if (!Severities.Contains((r.Severity ?? "").Trim().ToUpperInvariant()))
                throw new ArgumentException("Severity: HIGH | MEDIUM | LOW | SUGGESTION");
            if (r.Category is { Length: > 50 })
                throw new ArgumentException("Category 50 characters se chhoti honi chahiye");
            if (r.FilePattern is { Length: > 300 })
                throw new ArgumentException("FilePattern 300 characters se chhota hona chahiye");

            if (r.RepositoryId.HasValue &&
                !await _rules.RepositoryBelongsToOrganizationAsync(organizationId, r.RepositoryId.Value))
                throw new KeyNotFoundException("Repository nahi mili");
        }

        private static void Apply(ReviewRule rule, UpsertRuleRequest r)
        {
            rule.RepositoryId = r.RepositoryId;
            rule.Title = r.Title.Trim();
            rule.Description = r.Description.Trim();
            rule.Severity = r.Severity.Trim().ToUpperInvariant();
            rule.Category = string.IsNullOrWhiteSpace(r.Category) ? null : r.Category.Trim();
            rule.FilePattern = string.IsNullOrWhiteSpace(r.FilePattern) ? null : r.FilePattern.Trim();
            rule.IsEnabled = r.IsEnabled;
        }

        private static RuleDto Map(ReviewRule r) => new()
        {
            Id = r.Id,
            RepositoryId = r.RepositoryId,
            Title = r.Title,
            Description = r.Description,
            Severity = r.Severity,
            Category = r.Category,
            FilePattern = r.FilePattern,
            IsEnabled = r.IsEnabled,
            UpdatedAt = r.UpdatedAt
        };
    }
}
