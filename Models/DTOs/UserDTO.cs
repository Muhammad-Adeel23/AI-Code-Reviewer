using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Models.DTOs
{
    public class UserDto
    {
        public int Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }
        public string Role { get; set; } = string.Empty;
        public bool EmailVerified { get; set; }
        public string OrgRole { get; set; } = string.Empty;  // ← ADD
        public int OrgId { get; set; }                  // ← ADD
        public string OrgName { get; set; } = string.Empty;  // ← ADD
        public DateTime CreatedAt { get; set; }
    }
}
