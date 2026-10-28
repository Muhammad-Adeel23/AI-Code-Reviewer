using Microsoft.EntityFrameworkCore;
using Models.DTOs;
using Models.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Models.DTOs.TeamMembersDTO;

namespace Repository.Teams
{
    public class TeamRepository:ITeamRepository
    {
        private readonly AppDbContext _db;

        public TeamRepository(AppDbContext db)
        {
            _db = db;
        }

        // ─── Organization ─────────────────────────────────────────────────────────

        public async Task<Organization?> GetOrganizationByUserAsync(int userId)
        {
            return await _db.Organizations
                .FirstOrDefaultAsync(o =>
                    o.IsActive &&
                    o.OrganizationMembers.Any(m => m.UserId == userId && m.Status == "Active"));
        }

        public async Task<string> GetUserRoleAsync(int userId, int organizationId)
        {
            var member = await _db.OrganizationMembers
                .FirstOrDefaultAsync(m =>
                    m.UserId == userId &&
                    m.OrganizationId == organizationId &&
                    m.Status == "Active");

            return member?.Role ?? string.Empty;
        }

        // ─── Members ──────────────────────────────────────────────────────────────

        public async Task<TeamDto> GetTeamAsync(int organizationId)
        {
            var org = await _db.Organizations
                .Include(o => o.OrganizationMembers)
                    .ThenInclude(m => m.User)
                .FirstOrDefaultAsync(o => o.Id == organizationId)
                ?? throw new KeyNotFoundException("Organization does not exist");

            var pendingInvites = await _db.TeamInvitations
                
                .Where(i => i.OrganizationId == organizationId
                         && i.Status == "Pending"
                         && i.ExpiresAt > DateTime.UtcNow)
                .ToListAsync();
            var inviterIds = pendingInvites
       .Select(i => i.InvitedBy)
       .Distinct()
       .ToList();

            var inviters = await _db.Users
                .Where(u => inviterIds.Contains(u.Id))
                .Select(u => new { u.Id, u.FullName })
                .ToListAsync();

            return new TeamDto
            {
                OrganizationId = org.Id,
                OrganizationName = org.Name,
                Members = org.OrganizationMembers
            .Where(m => m.Status == "Active")
            .Select(m => new TeamMembersDTO
            {
                UserId = m.UserId,
                FullName = m.User.FullName,
                Email = m.User.Email,
                AvatarUrl = m.User.AvatarUrl,
                Role = m.Role,
                Status = m.Status,
                JoinedAt = m.JoinedAt ?? m.InvitedAt,
                IsOwner = org.OwnerId == m.UserId
            })
            .ToList(),
                PendingInvites = pendingInvites
            .Select(i => new PendingInvitationDto
            {
                Id = i.Id,
                Email = i.Email,
                Role = i.Role,
                InvitedByName = inviters
                    .FirstOrDefault(u => u.Id == i.InvitedBy)?.FullName ?? "Admin",
                ExpiresAt = i.ExpiresAt,
                CreatedAt = i.CreatedAt
            })
            .ToList()
            };
        }

        public async Task UpdateMemberRoleAsync(int organizationId, int userId, string role)
        {
            var member = await _db.OrganizationMembers
                .FirstOrDefaultAsync(m =>
                    m.OrganizationId == organizationId &&
                    m.UserId == userId)
                ?? throw new KeyNotFoundException("Member nahi mila");

            member.Role = role;
            await _db.SaveChangesAsync();
        }

        public async Task RemoveMemberAsync(int organizationId, int userId)
        {
            var member = await _db.OrganizationMembers
                .FirstOrDefaultAsync(m =>
                    m.OrganizationId == organizationId &&
                    m.UserId == userId)
                ?? throw new KeyNotFoundException("Member nahi mila");

            member.Status = "Suspended";
            await _db.SaveChangesAsync();
        }

        public async Task<bool> IsMemberAsync(int organizationId, int userId)
        {
            return await _db.OrganizationMembers
                .AnyAsync(m =>
                    m.OrganizationId == organizationId &&
                    m.UserId == userId &&
                    m.Status == "Active");
        }

        // ─── Invitations ──────────────────────────────────────────────────────────

        public async Task<bool> InvitationExistsAsync(int organizationId, string email)
        {
            // Active member ya pending invitation check karo
            var isMember = await _db.OrganizationMembers
                .AnyAsync(m =>
                    m.OrganizationId == organizationId &&
                    m.User.Email == email.ToLower() &&
                    m.Status == "Active");

            if (isMember) return true;

            return await _db.TeamInvitations
                .AnyAsync(i =>
                    i.OrganizationId == organizationId &&
                    i.Email == email.ToLower() &&
                    i.Status == "Pending" &&
                    i.ExpiresAt > DateTime.UtcNow);
        }

        public async Task<TeamInvitation> CreateInvitationAsync(TeamInvitation invitation)
        {
            _db.TeamInvitations.Add(invitation);
            await _db.SaveChangesAsync();
            return invitation;
        }

        public async Task<TeamInvitation?> GetInvitationByTokenAsync(string token)
        {
            return await _db.TeamInvitations
                .Include(i => i.Organization)
                .Include(i => i.InvitedByNavigation)
                .FirstOrDefaultAsync(i => i.Token == token);
        }
   
        public async Task AcceptInvitationAsync(string token, int userId)
        {
            var invitation = await _db.TeamInvitations
                .FirstOrDefaultAsync(i => i.Token == token)
                ?? throw new KeyNotFoundException("Invitation nahi mili");

            // Member add karo
            _db.OrganizationMembers.Add(new OrganizationMember
            {
                OrganizationId = invitation.OrganizationId,
                UserId = userId,
                Role = invitation.Role,
                InvitedBy = invitation.InvitedBy,
                JoinedAt = DateTime.UtcNow,
                InvitedAt = invitation.CreatedAt,
                Status = "Active"
            });


            // Invitation complete karo
            invitation.Status = "Accepted";
            invitation.AcceptedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
        }

        public async Task CancelInvitationAsync(int invitationId, int organizationId)
        {
            var invitation = await _db.TeamInvitations
                .FirstOrDefaultAsync(i =>
                    i.Id == invitationId &&
                    i.OrganizationId == organizationId)
                ?? throw new KeyNotFoundException("Invitation nahi mili");

            invitation.Status = "Cancelled";
            await _db.SaveChangesAsync();
        }

        public async Task<TeamInvitation?> GetInvitationByIdAsync(int id, int organizationId)
        {
            return await _db.TeamInvitations
                .FirstOrDefaultAsync(i =>
                    i.Id == id &&
                    i.OrganizationId == organizationId);
        }

        public async Task UpdateInvitationAsync(TeamInvitation invitation)
        {
            _db.TeamInvitations.Update(invitation);
            await _db.SaveChangesAsync();
        }
    }
}
