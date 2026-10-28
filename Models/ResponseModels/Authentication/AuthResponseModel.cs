using Models.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Models.ResponseModels.Authentication
{
    public class AuthResponseModel
    {
        public string Token { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public int ExpiresIn { get; set; } = 3600;          // seconds
        public DateTime ExpiresAt { get; set; }
        public UserDto User { get; set; } = null!;
    }
}
