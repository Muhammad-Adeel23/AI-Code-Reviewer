using Components.Common;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;
using Models.Entities;
using Repository.Email;
using Services.Email;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;

namespace AICodeReviewer.Services;

public class EmailVerificationService : IEmailVerificationService
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;
    private readonly IEmailSenderRepository _emailTemplate;
    private readonly ILogger<EmailVerificationService> _logger;

    //private readonly string _smtpHost;
    //private readonly int _smtpPort;
    //private readonly string _smtpUser;
    //private readonly string _smtpPass;
    //private readonly string _fromEmail;
    //private readonly string _fromName;
    private readonly string _frontendUrl;

    public EmailVerificationService(
        AppDbContext db,
        IConfiguration config,
        IEmailSenderRepository emailTemplate,
        ILogger<EmailVerificationService> logger)
    {
        _db = db;
        _config = config;
        _logger = logger;
        _emailTemplate = emailTemplate;
        //_smtpHost = config["Email:SmtpHost"] ?? "smtp.gmail.com";
        //_smtpPort = int.Parse(config["Email:SmtpPort"] ?? "587");
        //_smtpUser = config["Email:SmtpUser"] ?? throw new InvalidOperationException("Email:SmtpUser missing");
        //_smtpPass = config["Email:SmtpPass"] ?? throw new InvalidOperationException("Email:SmtpPass missing");
        //_fromEmail = config["Email:FromEmail"] ?? _smtpUser;
        //_fromName = config["Email:FromName"] ?? "AI Code Reviewer";
        _frontendUrl = config["App:FrontendUrl"] ?? "http://localhost:3000";
    }

    public async Task SendVerificationAsync(int userId, string email, string fullName)
    {
        // Purane unused tokens delete karo
        var oldTokens = await _db.RefreshTokens
            .Where(t => t.UserId == userId && !t.IsRevoked)
            .ToListAsync();
        _db.RefreshTokens.RemoveRange(oldTokens);

        var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        var hashedToken = HashToken(rawToken);

        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = userId,
            Token = hashedToken,
            ExpiresAt = DateTime.UtcNow.AddHours(24),
            IsRevoked = false,
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();

        var urlToken = Uri.EscapeDataString(rawToken);
        var verifyLink = $"{_frontendUrl}/verify-email?token={urlToken}";

        try
        {
            var template = await _emailTemplate.GetEmailTemplateById(
                (long)EmailTemplateEnum.VerficatationEmail);

            if (template != null)
            {
                var supportUrl = $"{_frontendUrl}/support";

                var body = template.TemplateBody
                    .Replace("{{UserName}}", fullName)
                    .Replace("{{VerificationLink}}", verifyLink)
                    .Replace("{{SupportUrl}}", supportUrl);

                await _emailTemplate.QueueEmailAsync(
                    email,
                    template.Subject,
                    body,
                    (long)EmailTemplateEnum.VerficatationEmail);

                _logger.LogInformation(
                    "Verification email queued successfully for UserId {UserId}",
                    userId);
            }
            else
            {
                _logger.LogWarning(
                    "Verification email template not found. UserId {UserId}",
                    userId);
            }
        }
        catch (Exception ex)
        {
            // Registration should not fail because email queue failed
            _logger.LogError(
                ex,
                "Failed to queue verification email for UserId {UserId}",
                userId);
        }

        _logger.LogInformation("Verification email sent → UserId: {Id}", userId);
    }

    public async Task<bool> VerifyEmailAsync(string rawToken)
    {
        var hashedToken = HashToken(rawToken);

        var tokenEntity = await _db.RefreshTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.Token == hashedToken);

        if (tokenEntity is null)
        {
            _logger.LogWarning("Invalid token");
            return false;
        }

        if (tokenEntity.IsRevoked)
        {
            _logger.LogWarning("Already used token. UserId: {Id}", tokenEntity.UserId);
            return false;
        }

        if (tokenEntity.ExpiresAt < DateTime.UtcNow)
        {
            _logger.LogWarning("Expired token. UserId: {Id}", tokenEntity.UserId);
            return false;
        }

        tokenEntity.IsRevoked = true;
        //tokenEntity.UsedAt = DateTime.UtcNow;
        tokenEntity.User.EmailVerified = true;
        tokenEntity.User.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        _logger.LogInformation("Email verified ✓ UserId: {Id}", tokenEntity.UserId);
        return true;
    }

    public async Task ResendVerificationAsync(string email)
    {
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower() && u.IsActive);

        if (user is null)
            throw new KeyNotFoundException("Yeh email registered nahi hai");

        if (user.EmailVerified)
            throw new InvalidOperationException("Yeh email pehle se verified hai");

        var recentToken = await _db.RefreshTokens
            .Where(t => t.UserId == user.Id && !t.IsRevoked)
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefaultAsync();

        if (recentToken is not null &&
            recentToken.CreatedAt > DateTime.UtcNow.AddMinutes(-5))
            throw new InvalidOperationException("5 minute baad dobara try karein");

        await SendVerificationAsync(user.Id, user.Email, user.FullName);
    }

//    private async Task SendVerificationEmailAsync(
//        string toEmail, string fullName, string verificationLink)
//    {
//        var subject = "Apna email verify karein — AI Code Reviewer";

//        var body = $@"
//<!DOCTYPE html>
//<html>
//<body style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; background: #f8fafc;'>
//  <div style='background: #1e293b; padding: 32px; border-radius: 12px; margin: 20px;'>
//    <h1 style='color: #818cf8; margin: 0 0 8px;'>AI Code Reviewer</h1>
//    <p style='color: #94a3b8; margin: 0;'>Email Verification</p>
//  </div>
//  <div style='padding: 32px 20px;'>
//    <h2 style='color: #1e293b;'>Salam {fullName}!</h2>
//    <p style='color: #374151; font-size: 16px; line-height: 1.6;'>
//      Shukriya register karne ke liye. Apna account activate karne ke liye
//      neeche button par click karein:
//    </p>
//    <div style='text-align: center; margin: 32px 0;'>
//      <a href='{verificationLink}'
//         style='background: #818cf8; color: white; padding: 14px 32px;
//                border-radius: 8px; text-decoration: none; font-size: 16px; font-weight: 500;'>
//        Email Verify Karein
//      </a>
//    </div>
//    <p style='color: #6b7280; font-size: 14px;'>
//      Ya yeh link copy karke browser mein paste karein:<br/>
//      <a href='{verificationLink}'
//         style='color: #818cf8; word-break: break-all;'>{verificationLink}</a>
//    </p>
//    <p style='color: #9ca3af; font-size: 13px; margin-top: 24px;'>
//      ⚠️ Yeh link 24 ghante mein expire ho jaega.<br/>
//      Agar aap ne register nahi kiya toh is email ko ignore karein.
//    </p>
//  </div>
//</body>
//</html>";

//        try
//        {
//            var message = new MimeMessage();
//            message.From.Add(new MailboxAddress(_fromName, _fromEmail));
//            message.To.Add(MailboxAddress.Parse(toEmail));
//            message.Subject = subject;

//            var builder = new BodyBuilder { HtmlBody = body };
//            message.Body = builder.ToMessageBody();

//            using var client = new MailKit.Net.Smtp.SmtpClient();
//            await client.ConnectAsync(_smtpHost, _smtpPort, SecureSocketOptions.StartTls);
//            await client.AuthenticateAsync(_smtpUser, _smtpPass);
//            await client.SendAsync(message);
//            await client.DisconnectAsync(true);

//            _logger.LogInformation("Email sent → {Email}", toEmail);
//        }
//        catch (Exception ex)
//        {
//            _logger.LogError(ex, "Email send karne mein masla → {Email}", toEmail);
//            throw;
//        }
//    }

    private static string HashToken(string rawToken)
    {
        var bytes = Encoding.UTF8.GetBytes(rawToken);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLower();
    }
}