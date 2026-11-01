using System;
using System.Collections.Generic;

namespace Models.Entities;

public partial class RepoIndexState
{
    public int Id { get; set; }

    public int RepositoryId { get; set; }

    public string Status { get; set; } = null!;

    public string? LastCommitSha { get; set; }

    public int FilesIndexed { get; set; }

    public int ChunksIndexed { get; set; }

    public DateTime? StartedAt { get; set; }

    public DateTime? LastIndexedAt { get; set; }

    public string? LastError { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual Repository Repository { get; set; } = null!;
}
