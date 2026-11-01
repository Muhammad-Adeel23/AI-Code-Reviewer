using System;
using System.Collections.Generic;

namespace Models.Entities;

public partial class ReviewIssue
{
    public int Id { get; set; }

    public int ReviewId { get; set; }

    public string Severity { get; set; } = null!;

    public string? FileName { get; set; }

    public int? LineNumber { get; set; }

    public string Description { get; set; } = null!;

    public string? Suggestion { get; set; }

    public string? Category { get; set; }

    public string Status { get; set; } = null!;

    public DateTime? StatusUpdatedAt { get; set; }

    public int? StatusUpdatedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public int? RuleId { get; set; }

    public bool PostedInline { get; set; }

    public string? Fingerprint { get; set; }

    public virtual Review Review { get; set; } = null!;

    public virtual User? StatusUpdatedByNavigation { get; set; }
}
