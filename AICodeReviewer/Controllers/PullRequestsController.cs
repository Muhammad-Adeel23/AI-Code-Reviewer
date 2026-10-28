using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Models.ApiResponseModels;
using Services.PullRequest;
using System.Security.Claims;
using static Models.DTOs.PullRequestDTO;

namespace AICodeReviewer.Controllers
{
    [ApiController]
    [Route("api")]
    [Authorize]
    [Produces("application/json")]
    public class PullRequestsController : BaseController
    {
        private readonly IPullRequestService _prService;

        public PullRequestsController(IPullRequestService prService)
        {
            _prService = prService;
        }

        // GET /api/pullrequests
        // Sab repos ki PRs — dashboard aur Pull Requests page ke liye
        [HttpGet("pullrequests")]
        public async Task<IActionResult> GetAllPullRequests(
            [FromQuery] string? status = null,
            [FromQuery] string? reviewStatus = null,
            [FromQuery] string? search = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                var userId = _userId;
                var orgRole = User.FindFirstValue("orgRole") ?? "";

                var filter = new PrFilterRequest
                {
                    Status = status,
                    ReviewStatus = reviewStatus,
                    Search = search,
                    Page = page,
                    PageSize = pageSize
                };

                var result = await _prService.GetAllPullRequestsAsync((int)userId, filter, orgRole);
                return Ok(ApiResponse<PagedResult<PullRequestListDto>>.Ok(result));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse<object>.Fail(ex.Message));
            }
        }

        // GET /api/repositories/{repoId}/pullrequests
        // Specific repo ki PRs
        [HttpGet("repositories/{repoId:int}/pullrequests")]
        public async Task<IActionResult> GetRepoPullRequests(
            int repoId,
            [FromQuery] string? status = null,
            [FromQuery] string? reviewStatus = null,
            [FromQuery] string? search = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                var userId = _userId;
                var orgRole = User.FindFirstValue("orgRole") ?? "";
                var filter = new PrFilterRequest
                {
                    Status = status,
                    ReviewStatus = reviewStatus,
                    Search = search,
                    Page = page,
                    PageSize = pageSize
                };

                var result = await _prService
                    .GetRepoPullRequestsAsync((int)userId, repoId, filter, orgRole);
                return Ok(ApiResponse<PagedResult<PullRequestListDto>>.Ok(result));
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, ApiResponse<object>.Fail(ex.Message));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse<object>.Fail(ex.Message));
            }
        }

        // GET /api/pullrequests/{id}
        // PR detail — diff viewer + review summary
        [HttpGet("pullrequests/{id:int}")]
        public async Task<IActionResult> GetPullRequestDetail(int id)
        {
            try
            {
                var userId = _userId;
                var result = await _prService.GetPullRequestDetailAsync((int)userId, id);
                return Ok(ApiResponse<PullRequestDetailDto>.Ok(result));
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, ApiResponse<object>.Fail(ex.Message));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse<object>.Fail(ex.Message));
            }
        }

        // POST /api/pullrequests/{id}/review
        // Manual review trigger
        [HttpPost("pullrequests/{id:int}/review")]
        public async Task<IActionResult> TriggerReview(int id)
        {
            try
            {
                var userId = _userId;
                var result = await _prService.TriggerReviewAsync((int)userId, id);
                return Ok(ApiResponse<ReviewStatusDto>.Ok(
                    result, "Review queue mein add ho gayi"));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ApiResponse<object>.Fail(ex.Message));
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, ApiResponse<object>.Fail(ex.Message));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse<object>.Fail(ex.Message));
            }
        }

        // GET /api/pullrequests/{id}/review/status
        // Review job status — frontend polling ke liye
        [HttpGet("pullrequests/{id:int}/review/status")]
        public async Task<IActionResult> GetReviewStatus(int id)
        {
            try
            {
                var userId = _userId;
                var result = await _prService.GetReviewStatusAsync((int)userId, id);
                return Ok(ApiResponse<ReviewStatusDto>.Ok(result));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse<object>.Fail(ex.Message));
            }
        }
    }
}