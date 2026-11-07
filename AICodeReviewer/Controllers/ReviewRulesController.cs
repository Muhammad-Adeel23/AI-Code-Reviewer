using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Models.ApiResponseModels;
using Models.DTOs;
using Services.Rule;
using System.Security.Claims;

namespace AICodeReviewer.Controllers
{
    /// <summary>
    /// Organization review rules: owners/admins define what every PR must follow (e.g. "controllers must not touch the
    /// database", "no hardcoded keys"); the AI checks each PR against them. Everyone in the org can read, only Owner/Admin can change.
    /// </summary>
    [ApiController]
    [Route("api/review-rules")]
    [Authorize]
    public class ReviewRulesController : BaseController
    {
        private readonly IReviewRuleService _rules;
        public ReviewRulesController(IReviewRuleService rules) => _rules = rules;

        private bool IsManager =>
            string.Equals(User.FindFirstValue("orgRole"), "Owner", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(User.FindFirstValue("orgRole"), "Admin", StringComparison.OrdinalIgnoreCase);

        private int OrgId => (int)_OrganizationId;

        // GET /api/review-rules?repositoryId=5     (repositoryId optional: org-wide + that repo; omit = all rules of the org)
        [HttpGet]
        public async Task<IActionResult> List([FromQuery] int? repositoryId) =>
            Ok(ApiResponse<List<RuleDto>>.Ok(await _rules.ListAsync(OrgId, repositoryId)));

        // GET /api/review-rules/templates          ready-made rules to add with one click
        [HttpGet("templates")]
        public IActionResult Templates() =>
            Ok(ApiResponse<IReadOnlyList<RuleTemplateDto>>.Ok(_rules.GetTemplates()));

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] UpsertRuleRequest request)
        {
            if (!IsManager) return StatusCode(403, ApiResponse<object>.Fail("Sirf Owner/Admin rules bana sakta hai"));
            var created = await _rules.CreateAsync(OrgId, (int)_userId, request);
            return StatusCode(201, ApiResponse<RuleDto>.Ok(created, "Rule ban gaya"));
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpsertRuleRequest request)
        {
            if (!IsManager) return StatusCode(403, ApiResponse<object>.Fail("Sirf Owner/Admin rules badal sakta hai"));
            return Ok(ApiResponse<RuleDto>.Ok(await _rules.UpdateAsync(OrgId, id, request), "Rule update ho gaya"));
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            if (!IsManager) return StatusCode(403, ApiResponse<object>.Fail("Sirf Owner/Admin rules hata sakta hai"));
            await _rules.DeleteAsync(OrgId, id);
            return Ok(ApiResponse.OkNoData("Rule delete ho gaya"));
        }
    }
}
