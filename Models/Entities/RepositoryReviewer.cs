using System;
using System.Collections.Generic;

namespace Models.Entities;

public partial class RepositoryReviewer
{
    public int Id { get; set; }

    public int RepositoryId { get; set; }

    public int ReviewerId { get; set; }

    public int? AssignedBy { get; set; }

    public DateTime AssignedAt { get; set; }

    public virtual User? AssignedByNavigation { get; set; }

    public virtual Repository Repository { get; set; } = null!;

    public virtual User Reviewer { get; set; } = null!;
}
