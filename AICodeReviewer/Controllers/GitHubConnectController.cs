using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Models.ApiResponseModels;
using Services.GitHub;
using System.Security.Claims;
using static Models.DTOs.GitHubDTO;

namespace AICodeReviewer.Controllers
{
    [ApiController]
    [Route("api/github")]
    [Produces("application/json")]
    public class GitHubConnectController : BaseController
    {
        private readonly IGitHubConnectService _connectService;
        private readonly IConfiguration _config;
        private readonly ILogger<GitHubConnectController> _logger;

        public GitHubConnectController(
            IGitHubConnectService connectService,
            IConfiguration config,
            ILogger<GitHubConnectController> logger)
        {
            _connectService = connectService;
            _config = config;
            _logger = logger;
        }
        [HttpGet("connect")]
        [Authorize]
        public async Task<IActionResult> GetConnectUrl()
        {
            try
            {
                var userId = _userId;
                var result = await _connectService.GetConnectUrlAsync((int)userId);
                return Ok(ApiResponse<GitHubConnectUrlResponse>.Ok(result));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Connect URL error");
                return BadRequest(ApiResponse<object>.Fail(ex.Message));
            }
        }

        // GET /api/github/connect/callback
        // GitHub yahan redirect karta hai — No [Authorize]
        [HttpGet("connect/callback")]
        public async Task<IActionResult> ConnectCallback(
            [FromQuery] string? code,
            [FromQuery] string? state,
            [FromQuery] string? error = null)
        {
            var frontendUrl = _config["App:FrontendUrl"] ?? "http://localhost:8080";

            // User ne GitHub par deny kiya
            if (!string.IsNullOrEmpty(error))
                return Redirect($"{frontendUrl}/settings?github=denied");

            if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state))
                return Redirect($"{frontendUrl}/settings?github=error&msg=missing_params");

            try
            {
                var result = await _connectService.HandleCallbackAsync(code, state);
                // Success — frontend dashboard par bhejo
                return Redirect(result.RedirectUrl);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning("Callback unauthorized: {Msg}", ex.Message);
                return Redirect($"{frontendUrl}/settings?github=error&msg=invalid_state");
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "Callback error");
                return Redirect(
                    $"{frontendUrl}/settings?github=error&msg={Uri.EscapeDataString(ex.Message)}");
            }
        }

        // GET /api/github/connection-status
        // Dashboard — connected hai ya nahi
        [HttpGet("connection-status")]
        [Authorize]
        public async Task<IActionResult> GetConnectionStatus()
        {
            try
            {
                var userId = _userId;
                var status = await _connectService.GetConnectionStatusAsync((int)userId);
                return Ok(ApiResponse<GitHubConnectionStatus>.Ok(status));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse<object>.Fail(ex.Message));
            }
        }

        // DELETE /api/github/disconnect
        [HttpDelete("disconnect")]
        [Authorize]
        public async Task<IActionResult> Disconnect()
        {
            try
            {
                var userId = _userId;
                await _connectService.DisconnectAsync((int)userId);
                return Ok(ApiResponse.OkNoData("GitHub disconnect ho gaya"));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse<object>.Fail(ex.Message));
            }
        }

        

    }
}
