using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Models.DTOs.GitHubDTO;

namespace Repository.GitHub
{
    public interface IGitHubConnectRepository
    {
        Task<string> SaveOAuthStateAsync(int userId);
        Task<int?> ValidateAndConsumeStateAsync(string state);
        Task<bool> IsGitHubAlreadyLinkedAsync(long githubId, int currentUserId);
        Task UpdateUserGitHubInfoAsync(int userId, long githubId, string githubLogin, string accessToken, string? avatarUrl);
        Task<GitHubConnectionStatus> GetConnectionStatusAsync(int userId);
        Task DisconnectAsync(int userId);
        Task<string?> GetUserGitHubTokenAsync(int userId);
        Task<Models.Entities.PullRequest?> ProcessPrEventAsync(string payload);
        bool VerifySignature(string payload, string signature, string secret);
        Task UpdateReviewStatusAsync(int pullRequestId, string status);
    }
}
