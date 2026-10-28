using System;
using System.Collections.Generic;

namespace Models.Entities;

public partial class PullRequestFile
{
    public int Id { get; set; }

    public int PullRequestId { get; set; }

    public string FileName { get; set; } = null!;

    public string Status { get; set; } = null!;

    public int Additions { get; set; }

    public int Deletions { get; set; }

    public string? Patch { get; set; }

    public virtual PullRequest PullRequest { get; set; } = null!;
}
