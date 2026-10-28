using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Models.DTOs.GitHubDTO;

namespace Services.GitHub
{
    public interface IGitHubConnectService
    {
        Task<GitHubConnectUrlResponse> GetConnectUrlAsync(int userId);
        Task<GitHubConnectResult> HandleCallbackAsync(string code, string state);
        Task<GitHubConnectionStatus> GetConnectionStatusAsync(int userId);
        Task DisconnectAsync(int userId);
        Task ProcessPrEventAsync(string payload);
        bool VerifySignature(string payload, string signature, string secret);
    }
}
