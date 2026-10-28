using DeviceDetectorNET;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Models.ApiResponseModels;
using Models.DTOs;
using Models.RequestModels;
using Models.RequestModels.AuthRequestModel;
using Models.RequestModels.ChangePassword;
using Models.RequestModels.ResetRequest;
using Models.RequestModels.VerifyEmail;
using Models.ResponseModels.Authentication;
using Services.Authentication;
using System.Runtime.CompilerServices;
using System.Security.Claims;

namespace AICodeReviewer.Controllers
{
    [ApiController]
    [Route("api/auth")]
    [Produces("application/json")]
    public class AuthenticationController : BaseController
    {
        private readonly IAuthenticationService _authService;
        private readonly ILogger<AuthenticationController> _logger;
        public AuthenticationController(
            IAuthenticationService authService, 
            ILogger<AuthenticationController> logger
            )
        {
            _authService = authService;
            _logger = logger;
        }
        [HttpPost("register")]
        [ProducesResponseType(typeof(ApiResponse<AuthResponseModel>), 201)]
        [ProducesResponseType(typeof(ApiResponse<object>), 400)]
        [ProducesResponseType(typeof(ApiResponse<object>), 409)]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();
                return BadRequest(ApiResponse<AuthResponseModel>.Fail(errors));
            }

            try
            {
                var ipAddress = GetIpAddress();
                var result = await _authService.RegisterAsync(request, ipAddress);
                return StatusCode(201, ApiResponse<AuthResponseModel>.Ok(result, "Account register successfully"));
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(ApiResponse<AuthResponseModel>.Fail(ex.Message));
            }
        }


        [HttpPost("verifyemail")]
        [ProducesResponseType(typeof(ApiResponse<object>), 200)]
        [ProducesResponseType(typeof(ApiResponse<object>), 400)]
        public async Task<IActionResult> VerifyEmail([FromBody] VerfiyEmailRequestModel request)
        {
            if (string.IsNullOrEmpty(request.Token))
                return BadRequest(ApiResponse<object>.Fail("UnAuthorized"));

            var isVerified = await _authService.VerifyEmailAsync(request.Token);

            if (!isVerified)
                return BadRequest(ApiResponse<object>.Fail(
                    "Token is invalid or Expired — please resend"));

            return Ok(ApiResponse.OkNoData("Email successfully verified ! You can login now."));
        }

        // ─── POST /api/auth/login ─────────────────────────────────────────────────
        /// <summary>Login karna — JWT + Refresh token milega</summary>
        [HttpPost("login")]
        [ProducesResponseType(typeof(ApiResponse<AuthResponseModel>), 200)]
        [ProducesResponseType(typeof(ApiResponse<object>), 400)]
        [ProducesResponseType(typeof(ApiResponse<object>), 401)]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();
                return BadRequest(ApiResponse<AuthResponseModel>.Fail(errors));
            }

            try
            {
                var ipAddress = GetIpAddress();
                var result = await _authService.LoginAsync(request, ipAddress);
                return Ok(ApiResponse<AuthResponseModel>.Ok(result, "Login successful"));
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ApiResponse<AuthResponseModel>.Fail(ex.Message));
            }
        }

        // ─── POST /api/auth/refresh ───────────────────────────────────────────────
        /// <summary>Expired access token ko refresh karna</summary>
        [HttpPost("refresh")]
        [ProducesResponseType(typeof(ApiResponse<AuthResponseModel>), 200)]
        [ProducesResponseType(typeof(ApiResponse<object>), 401)]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse<AuthResponseModel>.Fail("Refresh is required"));

            try
            {
                var ipAddress = GetIpAddress();
                var result = await _authService.RefreshTokenAsync(request.RefreshToken, ipAddress);
                return Ok(ApiResponse<AuthResponseModel>.Ok(result, "Token Refresh Successfully"));
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ApiResponse<AuthResponseModel>.Fail(ex.Message));
            }
        }

        // ─── POST /api/auth/logout ────────────────────────────────────────────────
        /// <summary>Logout — refresh token revoke karo</summary>
        [HttpPost("logout")]
        [Authorize]
        [ProducesResponseType(typeof(ApiResponse<object>), 200)]
        public async Task<IActionResult> Logout([FromBody] LogoutRequest request)
        {
            await _authService.LogoutAsync(request.RefreshToken);
            return Ok(ApiResponse.OkNoData("Logout successful"));
        }

        // ─── GET /api/auth/me ─────────────────────────────────────────────────────
        /// <summary>Current logged-in user ki info</summary>
        [HttpGet("me")]
        [Authorize]
        [ProducesResponseType(typeof(ApiResponse<UserDto>), 200)]
        [ProducesResponseType(401)]

//        Bearer xpl_070fcecffe8836119be5d0132f645868a283c553
        [HttpPost("forgotpassword")]
        [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ForgotPassword([FromBody] string email)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();

                return BadRequest(ApiResponse<string>.Fail(errors));
            }

            try
            {
                var (message, success) = await _authService.ForgotPassword(email);

                if (!success)
                    return NotFound(ApiResponse<string>.Fail(message));

                return Ok(ApiResponse<string>.Ok(message, "Password reset successful."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while resetting password.");

                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    ApiResponse<string>.Fail("An unexpected error occurred."));
            }
        }

        [HttpPost("resetpassword")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();

                return BadRequest(
                    ApiResponse<string>.Fail(errors));
            }

            var result =
                await _authService.ResetPasswordAsync(request);

            if (!result.Success)
            {
                return BadRequest(
                    ApiResponse<string>.Fail(result.Message));
            }

            return Ok(
                ApiResponse<string>.Ok(
                    result.Message,
                    result.Message));
        }


        [Authorize]
        [HttpPost("changepassword")]
        [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> ChangePassword(
    [FromBody] ChangePasswordRequest request)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();

                return BadRequest(ApiResponse<string>.Fail(errors));
            }

            var UserId = _userId;

            if (UserId == null || UserId<=0)
            {
                return Unauthorized(ApiResponse<string>.Fail("Invalid token."));
            }

            var ipAddress = GetIpAddress();

            var userAgent = Request.Headers.UserAgent.ToString();

            var detector = new DeviceDetector(userAgent);
            detector.Parse();

            var client = detector.GetClient();
            var os = detector.GetOs();

            string browser = client?.Match != null
                ? $"{client.Match.Name} {client.Match.Version}"
                : "Unknown Browser";

            string operatingSystem = os?.Match != null
                ? $"{os.Match.Name} {os.Match.Version}"
                : "Unknown Operating System";

            var result = await _authService.ChangePasswordAsync(
                _userId,
                request,
                ipAddress,
                browser,
                operatingSystem);
            if (!result.Success)
                return BadRequest(ApiResponse<string>.Fail(result.Message));

            return Ok(ApiResponse<string>.Ok(result.Message, result.Message));
        }
        
        private string GetIpAddress()
        {
            // X-Forwarded-For header (reverse proxy ke peeche hone par)
            if (Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor))
                return forwardedFor.ToString().Split(',')[0].Trim();

            return HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        }
    }
}
