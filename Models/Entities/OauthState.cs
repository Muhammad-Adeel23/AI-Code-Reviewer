using System;
using System.Collections.Generic;

namespace Models.Entities;

public partial class OauthState
{
    public int Id { get; set; }

    public string State { get; set; } = null!;

    public string Provider { get; set; } = null!;

    public int? UserId { get; set; }

    public DateTime ExpiresAt { get; set; }

    public bool IsUsed { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual User? User { get; set; }
}
