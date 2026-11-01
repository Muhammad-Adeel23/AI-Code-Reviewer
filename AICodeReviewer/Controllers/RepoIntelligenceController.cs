using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Models.ApiResponseModels;
using Models.DTOs;
using Services.Rag;
using System.Security.Claims;

namespace AICodeReviewer.Controllers
{
    [ApiController]
    [Route("api/repositories")]
    [Authorize]
    public class RepoIntelligenceController : BaseController
    {
        private readonly IRepoInsightService _insight;
        public RepoIntelligenceController(IRepoInsightService insight) => _insight = insight;

        private bool IsManager =>
            string.Equals(User.FindFirstValue("orgRole"), "Owner", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(User.FindFirstValue("orgRole"), "Admin", StringComparison.OrdinalIgnoreCase);

        private int OrgId => (int)_OrganizationId;

        // GET /api/repositories/{id}/index
        [HttpGet("{id:int}/index")]
        public async Task<IActionResult> GetIndexStatus(int id) =>
            Ok(ApiResponse<IndexStatusDto>.Ok(await _insight.GetIndexStatusAsync(OrgId, id)));

        // POST /api/repositories/{id}/index?force=false
        [HttpPost("{id:int}/index")]
        public async Task<IActionResult> StartIndexing(int id, [FromQuery] bool force = false)
        {
            if (!IsManager) return StatusCode(403, ApiResponse<object>.Fail("Sirf Owner/Admin index start kar sakta hai"));
            await _insight.StartIndexingAsync(OrgId, id, force);
            return Accepted(ApiResponse.OkNoData("Indexing queue mein add ho gayi"));
        }

        // PUT /api/repositories/{id}/review-settings
        [HttpPut("{id:int}/review-settings")]
        public async Task<IActionResult> UpdateReviewSettings(int id, [FromBody] ReviewSettingsRequest request)
        {
            if (!IsManager) return StatusCode(403, ApiResponse<object>.Fail("Sirf Owner/Admin settings badal sakta hai"));
            await _insight.UpdateSettingsAsync(OrgId, id, request);
            return Ok(ApiResponse.OkNoData("Settings update ho gayi"));
        }

        // POST /api/repositories/{id}/ask      { "question": "Where is auth handled?" }
        [HttpPost("{id:int}/ask")]
        public async Task<IActionResult> Ask(int id, [FromBody] AskRequest request, CancellationToken ct) =>
            Ok(ApiResponse<AskResponse>.Ok(await _insight.AskAsync(OrgId, id, request.Question, ct)));
    }
}
