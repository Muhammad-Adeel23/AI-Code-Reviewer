using System;
using System.Collections.Generic;

namespace Models.Entities;

public partial class UserSetting
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public bool EmailOnReviewComplete { get; set; }

    public bool EmailOnHighSeverity { get; set; }

    public bool InAppNotifications { get; set; }

    public bool AutoNotifyReviewer { get; set; }

    public bool AutoReviewOnPrOpen { get; set; }

    public string? SeverityThreshold { get; set; }

    public int MaxFilesPerReview { get; set; }

    public string? Theme { get; set; }

    public string? Timezone { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual User User { get; set; } = null!;
}
