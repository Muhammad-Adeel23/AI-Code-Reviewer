using Models.Entities;
using Models.RequestModels.ChangePassword;
using Models.RequestModels.ResetRequest;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Repository.Authentication
{
    public interface IAuthenticationRepository
    {
        Task<User?> GetByEmailAsync(string email);
        Task<User?> GetByIdAsync(int id);
        Task<bool> EmailExistsAsync(string email);
        Task<User> CreateAsync(User user);
        Task UpdateAsync(User user);

        // Refresh token management
        Task<RefreshToken> CreateRefreshTokenAsync(RefreshToken token);
        Task<RefreshToken?> GetRefreshTokenAsync(string token);
        Task RevokeRefreshTokenAsync(string token, string? replacedByToken = null);
        Task RevokeAllUserTokensAsync(int userId);

        Task CreateUserSettingsAsync(UserSetting settings);
        Task<bool> VerifyEmailAsync(string rawToken);
        Task<(string Message, bool Success)> ForgotPasswordAsync(string email);
        Task<(string Message, bool Success)> ResetPasswordAsync(ResetPasswordRequest request);
        Task<(string Message, bool Success)> ChangePasswordAsync(long userId,ChangePasswordRequest request, string ipAddress, string browser,string operatingSystem);  
            }
}
