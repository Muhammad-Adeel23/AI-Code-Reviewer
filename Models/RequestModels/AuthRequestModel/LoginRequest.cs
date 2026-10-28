using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Models.RequestModels.AuthRequestModel
{
    public class LoginRequest
    {
        [Required(ErrorMessage = "Email zaruri hai")]
        [EmailAddress(ErrorMessage = "Valid email dein")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password zaruri hai")]
        public string Password { get; set; } = string.Empty;
    }
}
