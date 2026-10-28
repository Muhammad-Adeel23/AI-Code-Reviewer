using Models.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Models.DTOs.TeamMembersDTO;

namespace Repository.Teams
{
    public interface ITeamRepository
    {
        // Organization
        Task<Organization?> GetOrganizationByUserAsync(int userId);
        Task<string> GetUserRoleAsync(int userId, int organizationId);

        // Members
        Task<TeamDto> GetTeamAsync(int organizationId);
        Task UpdateMemberRoleAsync(int organizationId, int userId, string role);
        Task RemoveMemberAsync(int organizationId, int userId);
        Task<bool> IsMemberAsync(int organizationId, int userId);

        // Invitations
        Task<bool> InvitationExistsAsync(int organizationId, string email);
        Task<TeamInvitation> CreateInvitationAsync(TeamInvitation invitation);
        Task<TeamInvitation?> GetInvitationByTokenAsync(string token);
        Task AcceptInvitationAsync(string token, int userId);
        Task CancelInvitationAsync(int invitationId, int organizationId);
        Task<TeamInvitation?> GetInvitationByIdAsync(int id, int organizationId);
        Task UpdateInvitationAsync(TeamInvitation invitation);
    }
}
