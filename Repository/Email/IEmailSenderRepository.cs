using Models.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Repository.Email
{
    public interface IEmailSenderRepository
    {
        //Task SendEmailAsync(string toEmail, string subject, string htmlBody);
        Task<EmailTemplateDTO?> GetEmailTemplateById(long templateEnum);
        Task QueueEmailAsync(string toEmail,string subject,string body,long templateSeqKey,string? cc = null,string? bcc = null);
        Task SendEmailAsync(string fromEmail, string toEmail, string subject, string body, string? cc = null, string? bcc = null);
    }
}
