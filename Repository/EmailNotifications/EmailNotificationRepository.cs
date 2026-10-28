using Components.Common;
using Microsoft.EntityFrameworkCore;
using Models.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Repository.EmailNotifications
{
    public class EmailNotificationRepository : IEmailNotificationRepository
    {
        private readonly AppDbContext _db;

        public EmailNotificationRepository(AppDbContext db)
        {
            _db = db;
        }
        public async Task<List<Models.Entities.EmailNotification>> GetPendingEmailsAsync()
        {
            var pendingEmails = await _db.EmailNotifications
       .Where(e => e.StatusId == (long)EmailNotificationEnum.Pending)
       .OrderBy(e => e.CreatedOn)
       .Take(10)
       .ToListAsync();

            var failedEmails = await _db.EmailNotifications
                .Where(e => e.StatusId == (long)EmailNotificationEnum.Failed && (e.Retries ?? 0) < 3)
                .OrderBy(e => e.CreatedOn)
                .Take(10)
                .ToListAsync();


            var result = pendingEmails.Union(failedEmails).ToList();

            return result;
        }
        public async Task UpdateEmailStatusAsync(Models.Entities.EmailNotification email)
        {
            _db.EmailNotifications.Update(email);
            await _db.SaveChangesAsync();
        }
    }
}
