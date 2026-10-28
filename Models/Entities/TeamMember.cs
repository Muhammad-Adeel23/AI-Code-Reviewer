using System;
using System.Collections.Generic;

namespace Models.Entities;

public partial class TeamMember
{
    public int Id { get; set; }

    public int TeamId { get; set; }

    public int UserId { get; set; }

    public string Role { get; set; } = null!;

    public int? InvitedBy { get; set; }

    public DateTime? JoinedAt { get; set; }

    public DateTime InvitedAt { get; set; }

    public string Status { get; set; } = null!;

    public virtual User? InvitedByNavigation { get; set; }

    public virtual Team Team { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
