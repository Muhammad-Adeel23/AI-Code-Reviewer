using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Models.Entities
{
    public class UserSettings
    {
        public int Id { get; set; }
        public int UserId { get; set; }

        // Notification preferences
        public bool EmailOnReviewComplete { get; set; } = true;
        public bool EmailOnHighSeverity { get; set; } = true;
        public bool InAppNotifications { get; set; } = true;
        public bool AutoNotifyReviewer { get; set; } = false;

        // AI settings
        public bool AutoReviewOnPrOpen { get; set; } = true;
        public string SeverityThreshold { get; set; } = "ALL";
        public int MaxFilesPerReview { get; set; } = 20;

        // Display
        public string Theme { get; set; } = "dark";
        public string Timezone { get; set; } = "UTC";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public User User { get; set; } = null!;
    }

}
