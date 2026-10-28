using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Models.ApiResponseModels;
using Services.AIReview;
using Services.Reviews;
using System.Security.Claims;
using static Models.DTOs.AIReviewDTO;

namespace AICodeReviewer.Controllers
{
    [ApiController]
    [Route("api/reviews")]
    [Authorize]
    public class ReviewsController : BaseController
    {
        private readonly IReviewService _reviewService;

        public ReviewsController(IReviewService reviewService)
        {
            _reviewService = reviewService;
        }

        // GET /api/reviews/pr/{prId}
        // Latest review result
        [HttpGet("pr/{prId:int}")]
        public async Task<IActionResult> GetLatestReview(int prId)
        {
            try
            {
                var userId = GetUserId();
                var result = await _reviewService.GetLatestReviewAsync(userId, prId);
                return Ok(ApiResponse<ReviewDetailDto>.Ok(result));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse<object>.Fail(ex.Message));
            }
        }

        // GET /api/reviews/pr/{prId}/history
        // All versions timeline
        [HttpGet("pr/{prId:int}/history")]
        public async Task<IActionResult> GetReviewHistory(int prId)
        {
            var userId = GetUserId();
            var result = await _reviewService.GetReviewHistoryAsync(userId, prId);
            return Ok(ApiResponse<List<ReviewHistoryDto>>.Ok(result));
        }

        // PUT /api/reviews/{reviewId}/issues/{issueId}/status
        // Issue status update karo
        [HttpPut("{reviewId:int}/issues/{issueId:int}/status")]
        public async Task<IActionResult> UpdateIssueStatus(
            int reviewId, int issueId,
            [FromBody] UpdateIssueStatusRequest request)
        {
            try
            {
                var userId = GetUserId();
                await _reviewService.UpdateIssueStatusAsync(
                    userId, issueId, request.Status);
                return Ok(ApiResponse.OkNoData("Issue status update ho gaya"));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse<object>.Fail(ex.Message));
            }
        }

        private int GetUserId() =>
            int.Parse(User.FindFirstValue("userId")
                ?? throw new UnauthorizedAccessException("Token invalid"));
    }
    public class UpdateIssueStatusRequest
    {
        public string Status { get; set; } = string.Empty;
        // Open | Fixed | Acknowledged
    }
}
