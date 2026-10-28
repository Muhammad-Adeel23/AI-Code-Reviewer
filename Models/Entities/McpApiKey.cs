using System;
using System.Collections.Generic;

namespace Models.Entities;

public partial class McpApiKey
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public string Name { get; set; } = null!;

    public string KeyHash { get; set; } = null!;

    public string KeyPrefix { get; set; } = null!;

    public bool IsActive { get; set; }

    public DateTime? LastUsedAt { get; set; }

    public string? LastUsedIp { get; set; }

    public int UsageCount { get; set; }

    public DateTime? RevokedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual User User { get; set; } = null!;
}
