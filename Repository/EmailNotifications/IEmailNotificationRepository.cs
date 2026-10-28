using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Repository.EmailNotifications
{
    public interface IEmailNotificationRepository
    {
        Task<List<Models.Entities.EmailNotification>> GetPendingEmailsAsync();
        Task UpdateEmailStatusAsync(Models.Entities.EmailNotification email);
    }
}
