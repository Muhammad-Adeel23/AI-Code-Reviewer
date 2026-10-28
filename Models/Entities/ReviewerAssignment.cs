using System;
using System.Collections.Generic;

namespace Models.Entities;

public partial class ReviewerAssignment
{
    public int Id { get; set; }

    public int ReviewId { get; set; }

    public int PullRequestId { get; set; }

    public int ReviewerId { get; set; }

    public int AssignedBy { get; set; }

    public string? Message { get; set; }

    public string Status { get; set; } = null!;

    public string? ReviewerComment { get; set; }

    public DateTime? RespondedAt { get; set; }

    public DateTime AssignedAt { get; set; }

    public virtual User AssignedByNavigation { get; set; } = null!;

    public virtual PullRequest PullRequest { get; set; } = null!;

    public virtual Review Review { get; set; } = null!;

    public virtual User Reviewer { get; set; } = null!;
}
