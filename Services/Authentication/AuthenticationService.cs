using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Models.DTOs;
using Models.Entities;
using Models.RequestModels.AuthRequestModel;
using Models.RequestModels.ChangePassword;
using Models.RequestModels.ResetRequest;
using Models.ResponseModels.Authentication;
using Repository.Authentication;
using Services.Email;
using Services.JWT;


namespace Services.Authentication
{
    public class AuthenticationService : IAuthenticationService
    {
        private readonly IEmailVerificationService _emailVerificationService;
        private readonly IAuthenticationRepository _authRepo;
        private readonly IJwtService _jwtService;
        private readonly ILogger<AuthenticationService> _logger;
        private readonly IConfiguration _config;
        private readonly AppDbContext _db;

        private readonly int _refreshTokenExpiryDays;
        public AuthenticationService(
            IAuthenticationRepository authRepo,
            AppDbContext appDbContext,
            IEmailVerificationService emailVerificationService,
            IJwtService jwtService,
            ILogger<AuthenticationService> logger,
            IConfiguration config
            )
        {
            _emailVerificationService = emailVerificationService; // ← YEH ADD KARO
            _authRepo = authRepo;
            _jwtService = jwtService;
           _db = appDbContext;
            _logger = logger;
            _config = config;
            _refreshTokenExpiryDays = int.Parse(config["Jwt:RefreshTokenExpiryDays"] ?? "30");
        }

        public async Task<AuthResponseModel> RegisterAsync(RegisterRequest request, string ipAddress)
        {
            // 1. Email already exists check
            if (await _authRepo.EmailExistsAsync(request.Email))
                throw new InvalidOperationException("User already exists with this email");

            var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password, workFactor: 12);

            var user = new User
            {
                FullName = request.FullName.Trim(),
                Email = request.Email.ToLower().Trim(),
                PasswordHash = passwordHash,
                Role = "Admin",
                IsActive = true,
                EmailVerified = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var createdUser = await _authRepo.CreateAsync(user);

            var settings = new UserSetting
            {
                UserId = createdUser.Id,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            await _authRepo.CreateUserSettingsAsync(settings);

            var org = new Organization
            {
                Name = request.OrganizationName.Trim(),
                Slug = GenerateSlug(request.OrganizationName),
                OwnerId = createdUser.Id,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _db.Organizations.Add(org);
            await _db.SaveChangesAsync();

            // ─── OWNER MEMBER ADD KARO ────────────────────────────────────────────
            _db.OrganizationMembers.Add(new OrganizationMember
            {
                OrganizationId = org.Id,
                UserId = createdUser.Id,
                Role = "Owner",
                JoinedAt = DateTime.UtcNow,
                InvitedAt = DateTime.UtcNow,
                Status = "Active"
            });
            await _db.SaveChangesAsync();
            await _emailVerificationService.SendVerificationAsync(
                createdUser.Id,
                createdUser.Email,
                createdUser.FullName);

            var (accessToken, refreshToken) = await GenerateAndSaveTokens(createdUser, ipAddress);

            _logger.LogInformation("New user registered: {Email} | Id: {Id}", createdUser.Email, createdUser.Id);

            var orgMember = await _db.OrganizationMembers
    .Include(m => m.Organization)
    .FirstOrDefaultAsync(m => m.UserId == user.Id && m.Status == "Active");

            return BuildAuthResponse(
                user, accessToken, refreshToken,
                orgMember?.Role ?? "",
                orgMember?.OrganizationId ?? 0,
                orgMember?.Organization.Name ?? "");
            // return BuildAuthResponse(createdUser, accessToken, refreshToken);
        }

        // ─── LOGIN ────────────────────────────────────────────────────────────────

        public async Task<AuthResponseModel> LoginAsync(LoginRequest request, string ipAddress)
        {
            // 1. User dhundho email se
            var user = await _authRepo.GetByEmailAsync(request.Email);

            // 2. User nahi mila ya password galat — SAME error dena security ke liye
            //    (attacker ko pata na chale ke email exist karta hai ya nahi)
            if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
                throw new UnauthorizedAccessException("Email or Password is Incorrect");

            // 3. Account active check
            if (!user.IsActive)
                throw new UnauthorizedAccessException("Account is deactivate, please contact to the admin");
            if(!user.EmailVerified)
                throw new UnauthorizedAccessException("Email is not Verified");


            // 4. Last login update karo
            user.LastLoginAt = DateTime.UtcNow;
            await _authRepo.UpdateAsync(user);

            // 5. Tokens generate karo
            var (accessToken, refreshToken) = await GenerateAndSaveTokens(user, ipAddress);

            var orgMember = await _db.OrganizationMembers
                           .Include(m => m.Organization)
                           .FirstOrDefaultAsync(m => m.UserId == user.Id && m.Status == "Active");

            _logger.LogInformation("User logged in: {Email} | Id: {Id}", user.Email, user.Id);

            return BuildAuthResponse(user, accessToken, refreshToken,
    orgMember?.Role ?? "",
    orgMember?.OrganizationId ?? 0,
    orgMember?.Organization.Name ?? "");
        }

        // ─── REFRESH TOKEN ────────────────────────────────────────────────────────

        public async Task<AuthResponseModel> RefreshTokenAsync(string refreshToken, string ipAddress)
        {
            // 1. Token DB mein dhundho
            var storedToken = await _authRepo.GetRefreshTokenAsync(refreshToken);

            if (storedToken is null)
                throw new UnauthorizedAccessException("Invalid refresh token");

            // 2. Token revoked check
            if (storedToken.IsRevoked)
            {
                // Security: agar revoked token use ho raha hai, sab tokens revoke karo
                _logger.LogWarning("Revoked refresh token used — possible token theft. UserId: {Id}", storedToken.UserId);
                await _authRepo.RevokeAllUserTokensAsync(storedToken.UserId);
                throw new UnauthorizedAccessException("Token already revoked — security breach detected");
            }

            // 3. Token expired check
            if (storedToken.ExpiresAt < DateTime.UtcNow)
                throw new UnauthorizedAccessException("Refresh token expire has expired — please login again");

            var user = storedToken.User;

            // 4. Old token revoke karo
            await _authRepo.RevokeRefreshTokenAsync(refreshToken);

            // 5. Naye tokens banao
            var (newAccessToken, newRefreshToken) = await GenerateAndSaveTokens(user, ipAddress);

            _logger.LogInformation("Token refreshed for UserId: {Id}", user.Id);

            return BuildAuthResponse(user, newAccessToken, newRefreshToken);
        }

        // ─── LOGOUT ───────────────────────────────────────────────────────────────

        public async Task LogoutAsync(string refreshToken)
        {
            await _authRepo.RevokeRefreshTokenAsync(refreshToken);
            _logger.LogInformation("User logged out — token revoked");
        }

        // ─── PRIVATE HELPERS ──────────────────────────────────────────────────────

        private async Task<(string accessToken, string refreshToken)> GenerateAndSaveTokens(
            User user, string ipAddress)
        {
            var orgMember = await _db.OrganizationMembers
      .Include(m => m.Organization)
      .FirstOrDefaultAsync(m => m.UserId == user.Id && m.Status == "Active");

            var orgRole = orgMember?.Role ?? "";
            var orgId = orgMember?.OrganizationId ?? 0;

            // Access token generate karo
            var accessToken = _jwtService.GenerateAccessToken(user,orgRole, orgId);

            // Refresh token generate karo aur save karo
            var refreshTokenValue = _jwtService.GenerateRefreshToken();
            var refreshTokenEntity = new RefreshToken
            {
                UserId = user.Id,
                Token = refreshTokenValue,
                ExpiresAt = DateTime.UtcNow.AddDays(_refreshTokenExpiryDays),
                CreatedByIp = ipAddress,
                CreatedAt = DateTime.UtcNow
            };

            await _authRepo.CreateRefreshTokenAsync(refreshTokenEntity);

            return (accessToken, refreshTokenValue);
        }

        private static AuthResponseModel BuildAuthResponse(User user, string accessToken, string refreshToken,
             string orgRole = "", int orgId = 0, string orgName = "")
        {
            return new AuthResponseModel
            {
                Token = accessToken,
                RefreshToken = refreshToken,
                ExpiresIn = 3600,
                ExpiresAt = DateTime.UtcNow.AddHours(1),
                User = new UserDto
                {
                    Id = user.Id,
                    FullName = user.FullName,
                    Email = user.Email,
                    AvatarUrl = user.AvatarUrl,
                    Role = user.Role,
                    EmailVerified = user.EmailVerified,
                    OrgRole = orgRole,   // ← ADD
                    OrgId = orgId,     // ← ADD
                    OrgName = orgName,   // ← ADD
                    CreatedAt = user.CreatedAt
                }
            };
        }
        private static string GenerateSlug(string name)
        {
            return new string(
                name.ToLower()
                    .Replace(" ", "-")
                    .Where(c => char.IsLetterOrDigit(c) || c == '-')
                    .ToArray());
        }
        public async Task<bool> VerifyEmailAsync(string rawToken)
        {
            return await _authRepo.VerifyEmailAsync(rawToken);
        }

        public async Task<(string, bool)> ForgotPassword(string email)
        {
            return await _authRepo.ForgotPasswordAsync(email);
        }
        public async Task<(string Message, bool Success)> ResetPasswordAsync(
    ResetPasswordRequest request)
        {
            return await _authRepo.ResetPasswordAsync(request);
        }

        public async Task<(string Message, bool Success)> ChangePasswordAsync(
    long userId,
    ChangePasswordRequest request,
    string ipAddress,
    string browser,
    string operatingSystem)
        {
            return await _authRepo.ChangePasswordAsync(userId, request, ipAddress, browser, operatingSystem);
        }
        }
}
