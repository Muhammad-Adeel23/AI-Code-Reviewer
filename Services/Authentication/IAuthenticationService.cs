using Models.RequestModels.AuthRequestModel;
using Models.RequestModels.ChangePassword;
using Models.RequestModels.ResetRequest;
using Models.ResponseModels.Authentication;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Services.Authentication
{
    public interface IAuthenticationService
    {
        Task<AuthResponseModel> RegisterAsync(RegisterRequest request, string ipAddress);
        Task<AuthResponseModel> LoginAsync(LoginRequest request, string ipAddress);
        Task<AuthResponseModel> RefreshTokenAsync(string refreshToken, string ipAddress);
        Task LogoutAsync(string refreshToken);
        Task<bool> VerifyEmailAsync(string rawToken);
        Task<(string, bool)> ForgotPassword(string email);
        Task<(string Message, bool Success)> ResetPasswordAsync(ResetPasswordRequest request);
        Task<(string Message, bool Success)> ChangePasswordAsync(
            long userId,
            ChangePasswordRequest request,
            string ipAddress,
            string browser,
            string operatingSystem);
    }
}
