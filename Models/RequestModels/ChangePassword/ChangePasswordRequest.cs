using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Models.RequestModels.ChangePassword
{
    public class ChangePasswordRequest
    {
        [Required]
        public string CurrentPassword { get; set; } = string.Empty;

        [Required]
        [MinLength(8)]
        [MaxLength(128)]
        public string NewPassword { get; set; } = string.Empty;

        [Required]
        [Compare(nameof(NewPassword))]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
    public class UpdateMemberRoleRequest
    {
        [Required]
        public string Role { get; set; } = string.Empty;
    }
    public class AcceptInvitationRequest
    {
        [Required]
        public string Token { get; set; } = string.Empty;
        public string? FullName { get; set; }
        public string? Password { get; set; }
    }
}
