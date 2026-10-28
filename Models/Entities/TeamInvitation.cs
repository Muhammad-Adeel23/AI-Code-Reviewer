using System;
using System.Collections.Generic;

namespace Models.Entities;

public partial class TeamInvitation
{
    public int Id { get; set; }

    public string Email { get; set; } = null!;

    public string Role { get; set; } = null!;

    public string Token { get; set; } = null!;

    public int InvitedBy { get; set; }

    public DateTime ExpiresAt { get; set; }

    public DateTime? AcceptedAt { get; set; }

    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public int OrganizationId { get; set; }

    public virtual User InvitedByNavigation { get; set; } = null!;

    public virtual Organization Organization { get; set; } = null!;
}
