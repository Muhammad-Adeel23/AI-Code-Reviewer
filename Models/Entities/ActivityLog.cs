using System;
using System.Collections.Generic;

namespace Models.Entities;

public partial class ActivityLog
{
    public int Id { get; set; }

    public int? UserId { get; set; }

    public int? RepositoryId { get; set; }

    public int? PullRequestId { get; set; }

    public int? ReviewId { get; set; }

    public string Type { get; set; } = null!;

    public string Status { get; set; } = null!;

    public string Message { get; set; } = null!;

    public string? ErrorDetail { get; set; }

    public string? IpAddress { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual PullRequest? PullRequest { get; set; }

    public virtual Repository? Repository { get; set; }

    public virtual Review? Review { get; set; }

    public virtual User? User { get; set; }
}
