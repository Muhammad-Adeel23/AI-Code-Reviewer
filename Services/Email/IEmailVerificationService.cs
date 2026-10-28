using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Services.Email
{
    public interface IEmailVerificationService
    {
        Task ResendVerificationAsync(string email);
        Task SendVerificationAsync(int userId, string email, string fullName);
        Task<bool> VerifyEmailAsync(string rawToken);
    }
}
