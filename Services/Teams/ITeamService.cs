using Models.RequestModels.ChangePassword;
using Models.RequestModels.InviteMember;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Models.DTOs.TeamMembersDTO;

namespace Services.Teams
{
    public interface ITeamService
    {
        Task<TeamDto> GetTeamAsync(int userId);
        Task InviteMemberAsync(int userId, InviteMemberRequest request);
        Task UpdateMemberRoleAsync(int userId, int targetUserId, string role);
        Task RemoveMemberAsync(int userId, int targetUserId);
        Task<InvitationInfoDto> GetInvitationInfoAsync(string token);
        Task AcceptInvitationAsync(string token, int userId);
        Task AcceptInvitationNewUserAsync(AcceptInvitationRequest request);
        Task ResendInvitationAsync(int userId, int invitationId);
        Task CancelInvitationAsync(int userId, int invitationId);
    }
}
