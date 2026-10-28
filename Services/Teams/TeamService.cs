using Components.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Models.Entities;
using Models.RequestModels.ChangePassword;
using Models.RequestModels.InviteMember;
using Repository.Email;
using Repository.Settings;
using Repository.Teams;
using System.Security.Cryptography;
using static Models.DTOs.TeamMembersDTO;

namespace Services.Teams
{
    public class TeamService : ITeamService
    {
        private readonly ITeamRepository _teamRepo;
        private readonly IEmailSenderRepository _emailService;
        private readonly AppDbContext _db;
        private readonly ISettingRepository _settingRepository;  // ← ADD
        private readonly IConfiguration _config;
        private readonly ILogger<TeamService> _logger;
        private readonly string _frontendUrl;

        public TeamService(
            ITeamRepository teamRepo,
            IEmailSenderRepository emailService,
            ISettingRepository settingRepository,
            AppDbContext db,
            IConfiguration config,
            ILogger<TeamService> logger)
        {
            _teamRepo = teamRepo;
            _emailService = emailService;
            _db = db;
            _settingRepository = settingRepository;
            _config = config;
            _logger = logger;
            _frontendUrl = config["App:FrontendUrl"] ?? "http://localhost:8080";
        }

        // ─── Team list ────────────────────────────────────────────────────────────

        public async Task<TeamDto> GetTeamAsync(int userId)
        {
            var org = await _teamRepo.GetOrganizationByUserAsync(userId)
                ?? throw new KeyNotFoundException("Organization nahi mili");

            return await _teamRepo.GetTeamAsync(org.Id);
        }

        // ─── Invite member ────────────────────────────────────────────────────────

        public async Task InviteMemberAsync(int userId, InviteMemberRequest request)
        {
            // 1. Organization lo
            var org = await _teamRepo.GetOrganizationByUserAsync(userId)
                ?? throw new KeyNotFoundException("Organization nahi mili");

            // 2. Permission check — sirf Owner/Admin invite kar sakta hai
            var role = await _teamRepo.GetUserRoleAsync(userId, org.Id);
            if (role is not ("Owner" or "Admin"))
                throw new UnauthorizedAccessException("Sirf Owner ya Admin invite kar sakta hai");

            // 3. Owner role invite nahi ho sakti
            if (request.Role == "Owner")
                throw new InvalidOperationException("Owner role invite nahi ki ja sakti");

            // 4. Already member ya pending check
            if (await _teamRepo.InvitationExistsAsync(org.Id, request.Email))
                throw new InvalidOperationException(
                    "Yeh email already member hai ya invitation pending hai");

            // 5. Secure token generate karo
            //var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
            var tokenBytes = RandomNumberGenerator.GetBytes(48);
            var token = Convert.ToBase64String(tokenBytes)
                .Replace('+', '-')
                .Replace('/', '_')
                .Replace("=", "");
            // 6. Invitation save karo
            var invitation = await _teamRepo.CreateInvitationAsync(new TeamInvitation
            {
                OrganizationId = org.Id,
                Email = request.Email.ToLower().Trim(),
                Role = request.Role,
                Token = token,
                InvitedBy = userId,
                ExpiresAt = DateTime.UtcNow.AddHours(48),
                Status = "Pending",
                CreatedAt = DateTime.UtcNow
            });

            // 7. Inviter ka naam lo
            var inviter = await _db.Users.FindAsync(userId);
            var inviterName = inviter?.FullName ?? "Team Admin";

            // 8. Email bhejo
            var acceptLink = $"{_frontendUrl}/accept-invite?token={Uri.EscapeDataString(token)}";
            try
            {
                var template = await _emailService.GetEmailTemplateById(
                    (long)EmailTemplateEnum.InviteMember); // ← apna enum value daalo

                if (template != null)
                {
                    var appUrl = _settingRepository.GetSettingsKeyValue(SystemVariables.AppURL);

                    var body = template.TemplateBody
                        .Replace("{{InviterName}}", inviterName)
                        .Replace("{{OrgName}}", org.Name)
                        .Replace("{{Role}}", request.Role)
                        .Replace("{{AcceptLink}}", acceptLink)
                        .Replace("{{AppUrl}}", appUrl);

                    await _emailService.QueueEmailAsync(
                        request.Email,
                        template.Subject,
                        body,
                        (long)EmailTemplateEnum.InviteMember);

                    _logger.LogInformation(
                        "Team invitation email queued for {Email}", request.Email);
                }
                else
                {
                    _logger.LogWarning(
                        "Team invitation template not found for {Email}", request.Email);
                }
            }
            catch (Exception ex)
            {
                // Email fail ho toh invitation fail mat karo
                _logger.LogError(ex, "Failed to queue invitation email for {Email}", request.Email);
            }

            _logger.LogInformation(
                "Invitation sent to {Email} for org {Org} with role {Role}",
                request.Email, org.Name, request.Role);
        }

        // ─── Invitation info (accept page ke liye) ────────────────────────────────

        public async Task<InvitationInfoDto> GetInvitationInfoAsync(string token)
        {
            var invitation = await _teamRepo.GetInvitationByTokenAsync(token);

            if (invitation is null)
                throw new KeyNotFoundException("Invitation does not exist");

            if (invitation.Status != "Pending")
                throw new InvalidOperationException(
                    invitation.Status == "Accepted"
                        ? "Invitation already accepted"
                        : "Invitation has canceled");

            if (invitation.ExpiresAt < DateTime.UtcNow)
                throw new InvalidOperationException(
                    "Invitation has expired — Contact to Owner to Resend an email");

            // Check karo user pehle se registered hai ya nahi
            var userExists = await _db.Users
                .AnyAsync(u => u.Email.ToLower() == invitation.Email.ToLower());

            return new InvitationInfoDto
            {
                OrganizationName = invitation.Organization.Name,
                InvitedByName = invitation.InvitedByNavigation.FullName,
                Role = invitation.Role,
                Email = invitation.Email,
                UserExists = userExists
            };
        }

        // ─── Accept invitation — existing user ───────────────────────────────────

        public async Task AcceptInvitationAsync(string token, int userId)
        {
            var invitation = await _teamRepo.GetInvitationByTokenAsync(token);

            if (invitation is null || invitation.Status != "Pending")
                throw new InvalidOperationException("Invalid ya expired invitation");

            if (invitation.ExpiresAt < DateTime.UtcNow)
                throw new InvalidOperationException("Invitation has expired");

            // User ka email invitation email se match karo
            var user = await _db.Users.FindAsync(userId)
                ?? throw new KeyNotFoundException("User nahi mila");

            if (user.Email.ToLower() != invitation.Email.ToLower())
                throw new UnauthorizedAccessException(
                    "Yeh invitation aapke email ke liye nahi hai");

            // Pehle se member check
            if (await _teamRepo.IsMemberAsync(invitation.OrganizationId, userId))
                throw new InvalidOperationException("You are already a member of this Organization");

            await _teamRepo.AcceptInvitationAsync(token, userId);

            _logger.LogInformation(
                "Invitation accepted by UserId: {Id} for org: {Org}",
                userId, invitation.OrganizationId);
        }

        // ─── Accept invitation — naya user ───────────────────────────────────────

        public async Task AcceptInvitationNewUserAsync(AcceptInvitationRequest request)
        {
            if (string.IsNullOrEmpty(request.FullName) || string.IsNullOrEmpty(request.Password))
                throw new ArgumentException("FullName and Password are required");

            var invitation = await _teamRepo.GetInvitationByTokenAsync(request.Token);

            if (invitation is null || invitation.Status != "Pending")
                throw new InvalidOperationException("Invalid or expired invitation");

            if (invitation.ExpiresAt < DateTime.UtcNow)
                throw new InvalidOperationException("Invitation has expired");

            // Naya user banao
            var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password, workFactor: 12);

            var newUser = new User
            {
                FullName = request.FullName.Trim(),
                Email = invitation.Email,
                PasswordHash = passwordHash,
                Role = "developer",
                IsActive = true,
                EmailVerified = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _db.Users.Add(newUser);
            await _db.SaveChangesAsync();

            // Default settings banao
            _db.UserSettings.Add(new UserSetting
            {
                UserId = newUser.Id,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();

            // Organization mein add karo
            await _teamRepo.AcceptInvitationAsync(request.Token, newUser.Id);

            _logger.LogInformation(
                "New user {Email} joined org {Org} via invitation",
                invitation.Email, invitation.OrganizationId);
        }

        // ─── Role update ──────────────────────────────────────────────────────────

        public async Task UpdateMemberRoleAsync(int userId, int targetUserId, string role)
        {
            var org = await _teamRepo.GetOrganizationByUserAsync(userId)
                ?? throw new KeyNotFoundException("Organization nahi mili");

            // Permission check
            var requesterRole = await _teamRepo.GetUserRoleAsync(userId, org.Id);
            if (requesterRole is not ("Owner" or "Admin"))
                throw new UnauthorizedAccessException("Permission nahi hai");

            // Owner ka role change nahi ho sakta
            if (org.OwnerId == targetUserId)
                throw new InvalidOperationException("Owner ka role change nahi kiya ja sakta");

            // Admin sirf Developer/Reviewer change kar sakta hai
            if (requesterRole == "Admin" && role == "Admin")
                throw new UnauthorizedAccessException("Admin kisi ko Admin nahi bana sakta");

            if (role == "Owner")
                throw new InvalidOperationException("Owner role assign nahi kiya ja sakta");

            await _teamRepo.UpdateMemberRoleAsync(org.Id, targetUserId, role);
        }

        // ─── Remove member ────────────────────────────────────────────────────────

        public async Task RemoveMemberAsync(int userId, int targetUserId)
        {
            var org = await _teamRepo.GetOrganizationByUserAsync(userId)
                ?? throw new KeyNotFoundException("Organization does not exist");

            var requesterRole = await _teamRepo.GetUserRoleAsync(userId, org.Id);
            if (requesterRole is not ("Owner" or "Admin"))
                throw new UnauthorizedAccessException("Permission nahi hai");

            // Owner remove nahi ho sakta
            if (org.OwnerId == targetUserId)
                throw new InvalidOperationException("Owner can't be remove");

            await _teamRepo.RemoveMemberAsync(org.Id, targetUserId);

            _logger.LogInformation(
                "Member {Target} removed from org {Org} by {By}",
                targetUserId, org.Id, userId);
        }

        // ─── Resend invitation ────────────────────────────────────────────────────

        public async Task ResendInvitationAsync(int userId, int invitationId)
        {
            var org = await _teamRepo.GetOrganizationByUserAsync(userId)
                ?? throw new KeyNotFoundException("Organization does not exist");

            var role = await _teamRepo.GetUserRoleAsync(userId, org.Id);
            if (role is not ("Owner" or "Admin"))
                throw new UnauthorizedAccessException("Permission nahi hai");

            var invitation = await _teamRepo.GetInvitationByIdAsync(invitationId, org.Id)
                ?? throw new KeyNotFoundException("Invitation nahi mili");

            // Token refresh karo
            var newToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
            invitation.Token = newToken;
            invitation.ExpiresAt = DateTime.UtcNow.AddHours(48);
            invitation.Status = "Pending";

            await _teamRepo.UpdateInvitationAsync(invitation);

            // Email dobara bhejo
            try
            {
                var inviter = await _db.Users.FindAsync(userId);
                var template = await _emailService.GetEmailTemplateById(
                    (long)EmailTemplateEnum.InviteMember);

                if (template != null)
                {
                    var appUrl = _settingRepository.GetSettingsKeyValue(SystemVariables.AppURL);
                    var acceptLink = $"{appUrl}/accept-invite?token={Uri.EscapeDataString(newToken)}";

                    var body = template.TemplateBody
                        .Replace("{{InviterName}}", inviter?.FullName ?? "Team Admin")
                        .Replace("{{OrgName}}", org.Name)
                        .Replace("{{Role}}", invitation.Role)
                        .Replace("{{AcceptLink}}", acceptLink)
                        .Replace("{{AppUrl}}", appUrl);

                    await _emailService.QueueEmailAsync(
                        invitation.Email,
                        template.Subject,
                        body,
                        (long)EmailTemplateEnum.InviteMember);

                    _logger.LogInformation(
                        "Invitation resend email queued for {Email}", invitation.Email);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to queue resend invitation email for {Email}", invitation.Email);
            }
        }

        // ─── Cancel invitation ────────────────────────────────────────────────────

        public async Task CancelInvitationAsync(int userId, int invitationId)
        {
            var org = await _teamRepo.GetOrganizationByUserAsync(userId)
                ?? throw new KeyNotFoundException("Organization does not exist");

            var role = await _teamRepo.GetUserRoleAsync(userId, org.Id);
            if (role is not ("Owner" or "Admin"))
                throw new UnauthorizedAccessException("You do not have permission to resend an email");

            await _teamRepo.CancelInvitationAsync(invitationId, org.Id);
        }
    }
}
