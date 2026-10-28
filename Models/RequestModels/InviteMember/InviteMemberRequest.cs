using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Models.RequestModels.InviteMember
{
    public class InviteMemberRequest
    {
        [Required(ErrorMessage = "Email is Requierd")]
        [EmailAddress(ErrorMessage = "Please enter a valid Email")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Role is requierd")]
        public string Role { get; set; } = "Developer";
        // Admin | Reviewer | Developer
    }
}
