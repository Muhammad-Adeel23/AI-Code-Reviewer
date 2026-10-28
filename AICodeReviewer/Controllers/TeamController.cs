using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Models.ApiResponseModels;
using Models.RequestModels.ChangePassword;
using Models.RequestModels.InviteMember;
using Services.Teams;
using System.Security.Claims;
using static Models.DTOs.TeamMembersDTO;

namespace AICodeReviewer.Controllers
{
    [ApiController]
    [Route("api/team")]
    [Authorize]
    [Produces("application/json")]
    public class TeamController : BaseController
    {
        private readonly ITeamService _teamService;

        public TeamController(ITeamService teamService)
        {
            _teamService = teamService;
        }

        // GET /api/team
        // Team members + pending invites list
        [HttpGet]
        public async Task<IActionResult> GetTeam()
        {
            try
            {
                var userId = _userId;
                var team = await _teamService.GetTeamAsync((int)userId);
                return Ok(ApiResponse<TeamDto>.Ok(team));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse<object>.Fail(ex.Message));
            }
        }

        // POST /api/team/invite
        // Member ko invite karo
        [HttpPost("invite")]
        public async Task<IActionResult> InviteMember([FromBody] InviteMemberRequest request)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();
                return BadRequest(ApiResponse<object>.Fail(errors));
            }

            try
            {
                var userId = _userId;
                await _teamService.InviteMemberAsync((int)userId, request);
                return Ok(ApiResponse.OkNoData(
                    $"Invitation {request.Email} has been sent successfully"));
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, ApiResponse<object>.Fail(ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(ApiResponse<object>.Fail(ex.Message));
            }
        }

        // GET /api/team/invite/{token}
        // Invitation info lo — accept page ke liye (public endpoint)
        [HttpGet("invite/{token}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetInvitationInfo(string token)
        {
            try
            {
                var decodedToken = Uri.UnescapeDataString(token);
                var info = await _teamService.GetInvitationInfoAsync(decodedToken);
                return Ok(ApiResponse<InvitationInfoDto>.Ok(info));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse<object>.Fail(ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ApiResponse<object>.Fail(ex.Message));
            }
        }

        // POST /api/team/invite/{token}/accept
        // Existing user — invitation accept kare
        [HttpPost("invite/{token}/accept")]
        [Authorize]
        public async Task<IActionResult> AcceptInvitation(string token)
        {
            try
            {
                var decodedToken = Uri.UnescapeDataString(token);

                var userId = _userId;
                await _teamService.AcceptInvitationAsync(decodedToken, (int)userId);
                return Ok(ApiResponse.OkNoData("Member included Successfully"));
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, ApiResponse<object>.Fail(ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ApiResponse<object>.Fail(ex.Message));
            }
        }

        // POST /api/team/invite/accept-new
        // Naya user — register + accept
        [HttpPost("invite/accept-new")]
        [AllowAnonymous]
        public async Task<IActionResult> AcceptInvitationNewUser(
            [FromBody] AcceptInvitationRequest request)
        {
            try
            {
                request.Token = Uri.UnescapeDataString(request.Token);
                await _teamService.AcceptInvitationNewUserAsync(request);
                return Ok(ApiResponse.OkNoData(
                    "Account has been Created."));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ApiResponse<object>.Fail(ex.Message));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse<object>.Fail(ex.Message));
            }
        }

        // PUT /api/team/members/{userId}/role
        // Member ka role change karo
        [HttpPut("members/{targetUserId:int}/role")]
        public async Task<IActionResult> UpdateMemberRole(
            int targetUserId,
            [FromBody] UpdateMemberRoleRequest request)
        {
            try
            {
                var userId = _userId;
                await _teamService.UpdateMemberRoleAsync((int)userId, targetUserId, request.Role);
                return Ok(ApiResponse.OkNoData("Role updated successfully"));
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, ApiResponse<object>.Fail(ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ApiResponse<object>.Fail(ex.Message));
            }
        }

        // DELETE /api/team/members/{userId}
        // Member remove karo
        [HttpDelete("members/{targetUserId:int}")]
        public async Task<IActionResult> RemoveMember(int targetUserId)
        {
            try
            {
                var userId = _userId;
                await _teamService.RemoveMemberAsync((int)userId, targetUserId);
                return Ok(ApiResponse.OkNoData("Member remove successfully"));
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, ApiResponse<object>.Fail(ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ApiResponse<object>.Fail(ex.Message));
            }
        }

        // POST /api/team/invite/{id}/resend
        // Invitation dobara bhejo
        [HttpPost("invite/{invitationId:int}/resend")]
        public async Task<IActionResult> ResendInvitation(int invitationId)
        {
            try
            {
                var userId = _userId;
                await _teamService.ResendInvitationAsync((int)userId, invitationId);
                return Ok(ApiResponse.OkNoData("Invitation resend successfully"));
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

        // DELETE /api/team/invite/{id}
        // Invitation cancel karo
        [HttpDelete("invite/{invitationId:int}")]
        public async Task<IActionResult> CancelInvitation(int invitationId)
        {
            try
            {
                var userId = _userId;
                await _teamService.CancelInvitationAsync((int)userId, invitationId);
                return Ok(ApiResponse.OkNoData("Invitation has been canceled"));
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
    }
}
