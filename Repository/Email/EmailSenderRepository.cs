using Components.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Models.DTOs;
using Models.Entities;
using Repository.Settings;
using System.Net;
using System.Net.Mail;


namespace Repository.Email
{
    public class EmailSenderRepository : IEmailSenderRepository
    {
        //Ov23libY7o9ZN0YOm022
        //52b7f9b415ba393dd20c7eb43adb4af6b97b21a0
        private readonly ILogger<EmailSenderRepository> _logger;
        private readonly ISettingRepository _settingRepository;
        private readonly AppDbContext _db;

        //private readonly string _smtpHost;
        //private readonly int _smtpPort;
        //private readonly string _smtpUser;
        //private readonly string _smtpPass;
        //private readonly string _fromEmail;
        //private readonly string _fromName;
        //private readonly string _frontendUrl;
        public EmailSenderRepository(
            ILogger<EmailSenderRepository> logger,
            IConfiguration config,
            ISettingRepository settingRepository,
            AppDbContext db
            )
        {
            _logger = logger;
            _settingRepository = settingRepository;
            _db = db;
            //_smtpHost = config["Email:SmtpHost"] ?? "smtp.gmail.com";
            //_smtpPort = int.Parse(config["Email:SmtpPort"] ?? "587");
            //_smtpUser = config["Email:SmtpUser"] ?? throw new InvalidOperationException("Email:SmtpUser missing");
            //_smtpPass = config["Email:SmtpPass"] ?? throw new InvalidOperationException("Email:SmtpPass missing");
            //_fromEmail = config["Email:FromEmail"] ?? _smtpUser;
            //_fromName = config["Email:FromName"] ?? "AI Code Reviewer";
            //_frontendUrl = config["App:FrontendUrl"] ?? "http://localhost:3000";

        }
        //public async Task SendEmailAsync(string fromEmail, string toEmail, string subject, string body, string? cc = null, string? bcc = null)
        //{
        //    try
        //    {
        //        var smtpClient = new System.Net.Mail.SmtpClient(
        //     _settingRepository.GetSettingsKeyValue(SystemVariables.SMTPServer),
        //     Convert.ToInt32(_settingRepository.GetSettingsKeyValue(SystemVariables.SMTPPort)))
        //        {
        //            EnableSsl = Convert.ToBoolean(_settingRepository.GetSettingsKeyValue(SystemVariables.IsTLS)),
        //            UseDefaultCredentials = false,
        //            Credentials = new NetworkCredential(
        //        fromEmail,
        //        _settingRepository.GetSettingsKeyValue(SystemVariables.SenderEmailPassword))
        //        };

        //        var mailMessage = new MailMessage(fromEmail, toEmail, subject, body);
        //        mailMessage.IsBodyHtml = true;
        //        if (!string.IsNullOrEmpty(cc))
        //        {
        //            foreach (var ccEmail in cc.Split(';', ',', StringSplitOptions.RemoveEmptyEntries))
        //                mailMessage.CC.Add(ccEmail.Trim());
        //        }


        //        if (!string.IsNullOrEmpty(bcc))
        //        {
        //            foreach (var bccEmail in bcc.Split(';', ',', StringSplitOptions.RemoveEmptyEntries))
        //                mailMessage.Bcc.Add(bccEmail.Trim());
        //        }
        //        await smtpClient.SendMailAsync(mailMessage);
        //    }
        //    catch (SmtpException smtpEx)
        //    {
        //        // SMTP-specific error (e.g., invalid credentials, host unreachable)
        //        Console.WriteLine($"SMTP Error: {smtpEx.Message}");
        //        throw;
        //    }
        //    catch (Exception ex)
        //    {
        //        // General error (e.g., argument null, format issue)
        //        Console.WriteLine($"Email Sending Error: {ex.Message}");
        //        throw;
        //    }
        //}


        //public async Task SendEmailAsync(string fromEmail, string password, string smtpServer, int smtpPort, bool enableSsl, string toEmail, string subject, string body)
        //{
        //    var smtpClient = new SmtpClient(smtpServer, smtpPort)
        //    {
        //        EnableSsl = enableSsl,
        //        UseDefaultCredentials = false,
        //        Credentials = new NetworkCredential(fromEmail, password)
        //    };

        //    var mailMessage = new MailMessage(fromEmail, toEmail, subject, body);
        //    mailMessage.IsBodyHtml = true;
        //    await smtpClient.SendMailAsync(mailMessage);
        //}

//        public async Task SendWelcomeEmailAsync(string toEmail, string fullName)
//        {
//            var subject = "Khush amdeed! — AI Code Reviewer";
//            var dashboardLink = $"{_frontendUrl}/dashboard";

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
//      <a href='{dashboardLink}' 
//         style='background: #818cf8; color: white; padding: 14px 32px; 
//                border-radius: 8px; text-decoration: none; font-size: 16px;'>
//        Dashboard Kholein
//      </a>
//    </div>
//  </div>
//</body>
//</html>";

//            await SendEmailAsync(toEmail, subject, body);
//        }
        //public async Task SendEmailAsync(string toEmail, string subject, string htmlBody)
        //{
        //    try
        //    {
        //        var message = new MimeMessage();
        //        message.From.Add(new MailboxAddress("test", "test"));
        //        message.To.Add(MailboxAddress.Parse(toEmail));
        //        message.Subject = subject;

        //        var builder = new BodyBuilder { HtmlBody = htmlBody };
        //        message.Body = builder.ToMessageBody();

        //        using var client = new SmtpClient();

        //        // Gmail: smtp.gmail.com:587 StartTls
        //        // Outlook: smtp.office365.com:587 StartTls
        //        await client.ConnectAsync(_smtpHost, _smtpPort, SecureSocketOptions.StartTls);
        //        await client.AuthenticateAsync(_smtpUser, _smtpPass);
        //        await client.SendAsync(message);
        //        await client.DisconnectAsync(true);

        //        _logger.LogInformation("Email sent to {Email} — Subject: {Subject}", toEmail, subject);
        //    }
        //    catch (Exception ex)
        //    {
        //        // Email fail hone par app crash mat karo — sirf log karo
        //        _logger.LogError(ex, "Email send karne mein masla aaya. To: {Email}", toEmail);
        //        throw;
        //    }
        //}



        //        public async Task SendVerificationEmailAsync(string toEmail, string fullName, string verificationLink)
//        {
//            var subject = "Apna email verify karein — AI Code Reviewer";

//            var body = $@"
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
//      neeche diye button par click karein:
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
//      <a href='{verificationLink}' style='color: #818cf8; word-break: break-all;'>{verificationLink}</a>
//    </p>
//    <p style='color: #9ca3af; font-size: 13px; margin-top: 24px;'>
//      ⚠️ Yeh link 24 ghante mein expire ho jaega.<br/>
//      Agar aap ne register nahi kiya toh is email ko ignore karein.
//    </p>
//  </div>
//</body>
//</html>";

//            await SendEmailAsync(toEmail, subject, body);
//        }

        public async Task QueueEmailAsync(
        string toEmail,
        string subject,
        string body,
        long templateSeqKey,
        string? cc = null,
        string? bcc = null)
        {
            try
            {
                var emailNotification = new EmailNotification
                {
                    To = toEmail,
                    Cc = cc,
                    Bcc = bcc,
                    Subject = subject,
                    Body = body,
                    StatusId = (long)EmailNotificationEnum.Pending,
                    Retries = 0,
                    CreatedOn = DateTime.UtcNow,
                    TemplateSeqKey = templateSeqKey
                };

                await _db.AddAsync(emailNotification);
                await _db.SaveChangesAsync();

                _logger.LogInformation(
                    "Email queued successfully for {ToEmail} at {Time}",
                    toEmail,
                    DateTime.UtcNow);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error occurred while queuing email for {ToEmail}",
                    toEmail);
            }
        }

        public async Task<EmailTemplateDTO?> GetEmailTemplateById(long templateEnum)
        {
            try
            {
                var template = await _db.InvitationTemplates
                    .AsNoTracking()
                    .Where(x => x.SeqKey == templateEnum)
                    .Select(x => new EmailTemplateDTO
                    {
                        Subject = x.Subject,
                        TemplateBody = x.TemplateBody
                    })
                    .FirstOrDefaultAsync();

                return template;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error occurred while fetching email template for SeqKey: {SeqKey}",
                    templateEnum);

                return null;
            }
        }

        public async Task SendEmailAsync(string fromEmail, string toEmail, string subject, string body, string? cc = null, string? bcc = null)
        {
            try
            {
                var smtpClient = new System.Net.Mail.SmtpClient(Convert.ToString(_settingRepository.GetSettingsKeyValue(SystemVariables.SMTPServer)), Convert.ToInt32(_settingRepository.GetSettingsKeyValue(SystemVariables.SMTPPort)))
                {
                    EnableSsl = Convert.ToBoolean(_settingRepository.GetSettingsKeyValue(SystemVariables.IsTLS)),
                    UseDefaultCredentials = false,
                    Credentials = new NetworkCredential(fromEmail, _settingRepository.GetSettingsKeyValue(SystemVariables.SenderEmailPassword))
                };

                var mailMessage = new MailMessage(fromEmail, toEmail, subject, body);
                mailMessage.IsBodyHtml = true;
                if (!string.IsNullOrEmpty(cc))
                {
                    foreach (var ccEmail in cc.Split(';', ',', StringSplitOptions.RemoveEmptyEntries))
                        mailMessage.CC.Add(ccEmail.Trim());
                }


                if (!string.IsNullOrEmpty(bcc))
                {
                    foreach (var bccEmail in bcc.Split(';', ',', StringSplitOptions.RemoveEmptyEntries))
                        mailMessage.Bcc.Add(bccEmail.Trim());
                }
                await smtpClient.SendMailAsync(mailMessage);
            }
            catch (SmtpException smtpEx)
            {
                // SMTP-specific error (e.g., invalid credentials, host unreachable)
                Console.WriteLine($"SMTP Error: {smtpEx.Message}");
                throw;
            }
            catch (Exception ex)
            {
                // General error (e.g., argument null, format issue)
                Console.WriteLine($"Email Sending Error: {ex.Message}");
                throw;
            }
        }

    }
}
