using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Models.DTOs
{
    public class TeamMembersDTO
    {
            public int UserId { get; set; }
            public string FullName { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
            public string? AvatarUrl { get; set; }
            public string Role { get; set; } = string.Empty;
            public string Status { get; set; } = string.Empty;
            public DateTime JoinedAt { get; set; }
            public bool IsOwner { get; set; }

        public class PendingInvitationDto
        {
            public int Id { get; set; }
            public string Email { get; set; } = string.Empty;
            public string Role { get; set; } = string.Empty;
            public string InvitedByName { get; set; } = string.Empty;
            public DateTime ExpiresAt { get; set; }
            public DateTime CreatedAt { get; set; }
        }

        public class TeamDto
        {
            public int OrganizationId { get; set; }
            public string OrganizationName { get; set; } = string.Empty;
            public List<TeamMembersDTO> Members { get; set; } = new();
            public List<PendingInvitationDto> PendingInvites { get; set; } = new();
        }

        public class InvitationInfoDto
        {
            public string OrganizationName { get; set; } = string.Empty;
            public string InvitedByName { get; set; } = string.Empty;
            public string Role { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
            public bool UserExists { get; set; }
            // true = sirf login karo, false = register karo
        }
    }
}
