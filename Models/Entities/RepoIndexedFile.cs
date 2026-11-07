using System;
using System.Collections.Generic;

namespace Models.Entities;

public partial class RepoIndexedFile
{
    public int Id { get; set; }

    public int RepositoryId { get; set; }

    public string FilePath { get; set; } = null!;

    public string ContentHash { get; set; } = null!;

    public int ChunkCount { get; set; }

    public DateTime IndexedAt { get; set; }

    public virtual Repository Repository { get; set; } = null!;
}
