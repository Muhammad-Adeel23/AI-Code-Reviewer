using Components.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Models.Entities;
using Models.RequestModels.ChangePassword;
using Models.RequestModels.ResetRequest;
using Org.BouncyCastle.Crypto.Generators;
using Repository.Email;
using Repository.Settings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Repository.Authentication
{
    public class AuthenticationRepository : IAuthenticationRepository
    {
        private readonly ILogger<AuthenticationRepository> _logger;
        private readonly IEmailSenderRepository _emailRepository;
        private readonly AppDbContext _db;
        private readonly ISettingRepository _settingRepository;

        public AuthenticationRepository(
            AppDbContext db,
            ILogger<AuthenticationRepository> logger,
            IEmailSenderRepository emailSenderRepository,
            ISettingRepository settingRepository
            )
        {
            _db = db;
            _emailRepository = emailSenderRepository;
            _logger = logger;
            _settingRepository = settingRepository;
        }
        public async Task<User?> GetByEmailAsync(string email)
        {
            return await _db.Users
                .Include(u => u.UserSetting)
                .FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower() && u.IsActive);
        }

        public async Task<User?> GetByIdAsync(int id)
        {
            return await _db.Users
                .Include(u => u.UserSetting)
                .FirstOrDefaultAsync(u => u.Id == id && u.IsActive);
        }

        public async Task<bool> EmailExistsAsync(string email)
        {
            try
            {



                return await _db.Users
                    .AnyAsync(u => u.Email.ToLower() == email.ToLower());
            }
            catch (Exception ex)
            {

                throw;
            }

        }

        public async Task<User> CreateAsync(User user)
        {
            _db.Users.Add(user);
            await _db.SaveChangesAsync();
            return user;
        }

        public async Task UpdateAsync(User user)
        {
            user.UpdatedAt = DateTime.UtcNow;
            _db.Users.Update(user);
            await _db.SaveChangesAsync();
        }

        // ─── REFRESH TOKEN ────────────────────────────────────────────────────────

        public async Task<RefreshToken> CreateRefreshTokenAsync(RefreshToken token)
        {
            _db.RefreshTokens.Add(token);
            await _db.SaveChangesAsync();
            return token;
        }

        public async Task<RefreshToken?> GetRefreshTokenAsync(string token)
        {
            return await _db.RefreshTokens
                .Include(rt => rt.User)
                .ThenInclude(u => u.UserSetting)
                .FirstOrDefaultAsync(rt => rt.Token == token);
        }

        public async Task RevokeRefreshTokenAsync(string token, string? replacedByToken = null)
        {
            var rt = await _db.RefreshTokens.FirstOrDefaultAsync(x => x.Token == token);
            if (rt is null) return;

            rt.IsRevoked = true;
            rt.RevokedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        public async Task RevokeAllUserTokensAsync(int userId)
        {
            var tokens = await _db.RefreshTokens
                .Where(rt => rt.UserId == userId && !rt.IsRevoked)
                .ToListAsync();

            foreach (var token in tokens)
            {
                token.IsRevoked = true;
                token.RevokedAt = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync();
        }

        public async Task<bool> VerifyEmailAsync(string rawToken)
        {
            var hashedToken = HashToken(rawToken);

            var tokenEntity = await _db.RefreshTokens
                .Include(t => t.User)
                .FirstOrDefaultAsync(t => t.Token == rawToken);

            // Token nahi mila
            if (tokenEntity is null)
            {
                _logger.LogWarning("Invalid verification token used");
                return false;
            }

            // Token already use ho chuka hai
            if (tokenEntity.IsRevoked)
            {
                _logger.LogWarning("Already used verification token. UserId: {Id}", tokenEntity.UserId);
                return false;
            }

            // Token expire ho gaya
            if (tokenEntity.ExpiresAt < DateTime.UtcNow)
            {
                _logger.LogWarning("Expired verification token. UserId: {Id}", tokenEntity.UserId);
                return false;
            }

            // Token valid — user verify karo
            tokenEntity.IsRevoked = true;
            tokenEntity.RevokedAt = DateTime.UtcNow;
            tokenEntity.User.EmailVerified = true;
            tokenEntity.User.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            try
            {
                var template = await _emailRepository.GetEmailTemplateById(
                    (long)EmailTemplateEnum.WelcomeEmail);

                if (template != null)
                {
                    var appUrl = _settingRepository.GetSettingsKeyValue(SystemVariables.AppURL);

                    var body = template.TemplateBody
                        .Replace("{{UserName}}", tokenEntity.User.FullName)
                        .Replace("{{DashboardUrl}}", $"{appUrl}/dashboard")
                        .Replace("{{SupportUrl}}", $"{appUrl}/support");

                    await _emailRepository.QueueEmailAsync(
                        tokenEntity.User.Email,
                        template.Subject,
                        body,
                        (long)EmailTemplateEnum.WelcomeEmail);

                    _logger.LogInformation(
                        "Welcome email queued successfully for UserId {UserId}",
                        tokenEntity.UserId);
                }
                else
                {
                    _logger.LogWarning(
                        "Welcome email template not found. UserId: {UserId}",
                        tokenEntity.UserId);
                }
            }
            catch (Exception ex)
            {
                // Email failure should NEVER fail email verification
                _logger.LogError(
                    ex,
                    "Failed to queue welcome email for UserId {UserId}",
                    tokenEntity.UserId);
            }

            _logger.LogInformation("Email verified for UserId: {Id}", tokenEntity.UserId);
            return true;
        }


        // ─── USER SETTINGS ────────────────────────────────────────────────────────

        public async Task CreateUserSettingsAsync(UserSetting settings)
        {
            _db.UserSettings.Add(settings);
            await _db.SaveChangesAsync();
        }
        private static string HashToken(string rawToken)
        {
            var bytes = Encoding.UTF8.GetBytes(rawToken);
            var hash = SHA256.HashData(bytes);
            return Convert.ToHexString(hash).ToLower();
        }



//        public async Task SendWelcomeEmailAsync(string toEmail, string fullName)
//        {
//            var subject = "Khush amdeed! — AI Code Reviewer";
//           // var dashboardLink = $"{_frontendUrl}/dashboard";

//            var body = $@"
//<!DOCTYPE html>
//<html>
//<body style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;'>
//  <div style='background: #1e293b; padding: 32px; border-radius: 12px; margin: 20px;'>
//    <h1 style='color: #818cf8; margin: 0;'>AI Code Reviewer</h1>
//  </div>
//  <div style='padding: 32px 20px;'>
//    <h2 style='color: #1e293b;'>🎉 Khush amdeed, {fullName}!</h2>
//    <p style='color: #374151; font-size: 16px; line-height: 1.6;'>
//      Aapka account successfully verify ho gaya. Ab aap:
//    </p>
//    <ul style='color: #374151; font-size: 15px; line-height: 2;'>
//      <li>GitHub repositories connect kar sakte hain</li>
//      <li>Pull Requests ki AI review karwa sakte hain</li>
//      <li>Team members invite kar sakte hain</li>
//    </ul>
//    <div style='text-align: center; margin: 32px 0;'>
//      <a href='""dashboardLink' 
//         style='background: #818cf8; color: white; padding: 14px 32px; 
//                border-radius: 8px; text-decoration: none; font-size: 16px;'>
//        Dashboard Kholein
//      </a>
//    </div>
//  </div>
//</body>
//</html>";

//            await _emailRepository.SendEmailAsync(toEmail, subject, body);
//        }

        public async Task<(string Message, bool Success)> ForgotPasswordAsync(string email)
        {
            const string responseMessage =
                "If an account with this email exists, a password reset link has been sent.";

            try
            {
                var user = await _db.Users
                    .FirstOrDefaultAsync(x => x.Email == email);

                if (user == null)
                {
                    _logger.LogInformation(
                        "Forgot password requested for non-existing email {Email}",
                        email);

                    return (responseMessage, true);
                }

                var oldTokens = await _db.PasswordResetTokens
                    .Where(x =>
                        x.UserId == user.Id &&
                        x.UsedAt == null)
                    .ToListAsync();

                foreach (var token in oldTokens)
                {
                    token.UsedAt = DateTime.UtcNow;
                }

                var rawToken = Helper.GenerateToken();

                var tokenHash = Helper.HashToken(rawToken);

                var passwordReset = new PasswordResetToken
                {
                    UserId = user.Id,
                    TokenHash = tokenHash,
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddMinutes(30),
                    UsedAt = null
                };

                await _db.PasswordResetTokens.AddAsync(passwordReset);

                await _db.SaveChangesAsync();

                string appUrl = _settingRepository.GetSettingsKeyValue(SystemVariables.AppURL);

                string resetLink =
                    $"{appUrl}/reset-password?token={Uri.EscapeDataString(rawToken)}";

                var template =
                    await _emailRepository.GetEmailTemplateById(
                        (long)EmailTemplateEnum.ResetPassword);

                if (template != null)
                {
                    string body = template.TemplateBody
                        .Replace("{{UserName}}", user.FullName ?? user.Email)
                        .Replace("{{ResetLink}}", resetLink);

                    await _emailRepository.QueueEmailAsync(
                        user.Email,
                        template.Subject,
                        body,
                        (long)EmailTemplateEnum.ResetPassword);

                    _logger.LogInformation(
                        "Password reset email queued for {Email}",
                        user.Email);
                }
                else
                {
                    _logger.LogWarning(
                        "Password reset template not found.");
                }

                return (responseMessage, true);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Forgot password failed for {Email}",
                    email);

                return ("Something went wrong.", false);
            }
        }

        public async Task<(string Message, bool Success)> ResetPasswordAsync(
    ResetPasswordRequest request)
        {
            try
            {
                var tokenHash =
                    Helper.HashToken(request.Token);

                var resetToken =
                    await _db.PasswordResetTokens
                    .Include(x => x.User)
                    .FirstOrDefaultAsync(x =>
                        x.TokenHash == tokenHash);

                if (resetToken == null)
                {
                    return ("Invalid reset link.", false);
                }

                if (resetToken.UsedAt != null)
                {
                    return ("Reset link has already been used.", false);
                }

                if (resetToken.ExpiresAt < DateTime.UtcNow)
                {
                    return ("Reset link has expired.", false);
                }

                resetToken.User.PasswordHash =
                    BCrypt.Net.BCrypt.HashPassword(request.NewPassword);

                resetToken.UsedAt = DateTime.UtcNow;

                var otherTokens =
                    await _db.PasswordResetTokens
                    .Where(x =>
                        x.UserId == resetToken.UserId &&
                        x.UsedAt == null)
                    .ToListAsync();

                foreach (var item in otherTokens)
                {
                    item.UsedAt = DateTime.UtcNow;
                }

                await _db.SaveChangesAsync();

                _logger.LogInformation(
                    "Password reset successfully for {Email}",
                    resetToken.User.Email);

                return ("Password has been reset successfully.", true);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Reset password failed.");

                return ("Something went wrong.", false);
            }
        }
        public async Task<(string Message, bool Success)> ChangePasswordAsync(
    long userId,
    ChangePasswordRequest request,
    string ipAddress,
    string browser,
    string operatingSystem)
        {
            try
            {
                var user = await GetByIdAsync((int)userId); 

                if (user == null)
                    return ("User not found.", false);


                if (!BCrypt.Net.BCrypt.Verify(
                    request.CurrentPassword,
                    user.PasswordHash))
                {
                    return ("Current password is incorrect.", false);
                }


                if (BCrypt.Net.BCrypt.Verify(
                    request.NewPassword,
                    user.PasswordHash))
                {
                    return ("New password cannot be same as current password.", false);
                }


                user.PasswordHash =
                    BCrypt.Net.BCrypt.HashPassword(
                        request.NewPassword,
                        workFactor: 12);

                user.UpdatedAt = DateTime.UtcNow;


                var refreshTokens =
                    await _db.RefreshTokens
                    .Where(x =>
                        x.UserId == user.Id &&
                        !x.IsRevoked)
                    .ToListAsync();

                foreach (var token in refreshTokens)
                {
                    token.IsRevoked = true;
                    token.RevokedAt = DateTime.UtcNow;
                }

                await _db.SaveChangesAsync();

                try
                {
                    var template = await _emailRepository.GetEmailTemplateById(
                        (long)EmailTemplateEnum.PasswordChanged);

                    if (template != null)
                    {
                        var appUrl = _settingRepository.GetSettingsKeyValue(SystemVariables.AppURL);

                        var body = template.TemplateBody
                            .Replace("{{UserName}}", user.FullName)
                            .Replace("{{ChangedAt}}", DateTime.UtcNow.ToString("dd MMM yyyy HH:mm 'UTC'"))
                            .Replace("{{IPAddress}}", ipAddress ?? "Unknown")
                            .Replace("{{Browser}}", browser ?? "Unknown")
                            .Replace("{{OperatingSystem}}", operatingSystem ?? "Unknown")
                            .Replace("{{LoginUrl}}", $"{appUrl}/login")
                            .Replace("{{SupportUrl}}", $"{appUrl}/support");

                        await _emailRepository.QueueEmailAsync(
                            user.Email,
                            template.Subject,
                            body,
                            (long)EmailTemplateEnum.PasswordChanged);

                        _logger.LogInformation(
                            "Password changed notification queued for {Email}",
                            user.Email);
                    }
                }
                catch (Exception ex)
                {
                    // Email failure should NEVER fail password change
                    _logger.LogError(
                        ex,
                        "Failed to queue password changed notification for {Email}",
                        user.Email);
                }


                _logger.LogInformation(
                    "Password changed successfully for UserId {UserId}",
                    user.Id);



                return (
                    "Password changed successfully. Please login again.",
                    true);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to change password for UserId {UserId}",
                    userId);

                return (
                    "Unable to change password.",
                    false);
            }
        }
        public static string GenerateRandomPassword(int length = 12)
         {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%^&*";

        var password = new char[length];

        for (int i = 0; i < length; i++)
        {
            password[i] = chars[RandomNumberGenerator.GetInt32(chars.Length)];
        }

        return new string(password);
    }

    }
}
