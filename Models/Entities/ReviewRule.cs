using System;
using System.Collections.Generic;

namespace Models.Entities;

public partial class ReviewRule
{
    public int Id { get; set; }

    public int OrganizationId { get; set; }

    public int? RepositoryId { get; set; }

    public string Title { get; set; } = null!;

    public string Description { get; set; } = null!;

    public string Severity { get; set; } = null!;

    public string? Category { get; set; }

    public string? FilePattern { get; set; }

    public bool IsEnabled { get; set; }

    public int? CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual Organization Organization { get; set; } = null!;

    public virtual Repository? Repository { get; set; }
}
