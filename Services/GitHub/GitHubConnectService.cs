using Hangfire;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Models.Entities;
using Repository.GitHub;
using Services.AiReviewJob;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using static Models.DTOs.GitHubDTO;

namespace Services.GitHub
{
    public class GitHubConnectService : IGitHubConnectService
    {
        private readonly IGitHubConnectRepository _repo;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _config;
        private readonly ILogger<GitHubConnectService> _logger;

        private readonly string _clientId;
        private readonly string _clientSecret;
        private readonly string _redirectUri;
        private readonly string _frontendUrl;

        public GitHubConnectService(
            IGitHubConnectRepository repo,
            IHttpClientFactory httpClientFactory,
            IConfiguration config,
            ILogger<GitHubConnectService> logger)
        {
            _repo = repo;
            _httpClientFactory = httpClientFactory;
            _config = config;
            _logger = logger;

            _clientId = config["GitHub:ClientId"] ?? throw new InvalidOperationException("GitHub:ClientId missing");
            _clientSecret = config["GitHub:ClientSecret"] ?? throw new InvalidOperationException("GitHub:ClientSecret missing");
            _redirectUri = config["GitHub:ConnectCallbackUri"] ?? throw new InvalidOperationException("GitHub:ConnectCallbackUri missing");
            _frontendUrl = config["App:FrontendUrl"] ?? "http://localhost:8080";
        }

        // ─── URL banao ────────────────────────────────────────────────────────

        public async Task<GitHubConnectUrlResponse> GetConnectUrlAsync(int userId)
        {
            var state = await _repo.SaveOAuthStateAsync(userId);
            var scopes = "read:user user:email repo admin:repo_hook";

            var url = "https://github.com/login/oauth/authorize" +
                      $"?client_id={_clientId}" +
                      $"&redirect_uri={Uri.EscapeDataString(_redirectUri)}" +
                      $"&scope={Uri.EscapeDataString(scopes)}" +
                      $"&state={state}";

            return new GitHubConnectUrlResponse { Url = url, State = state };
        }

        // ─── Callback handle karo ─────────────────────────────────────────────

        public async Task<GitHubConnectResult> HandleCallbackAsync(string code, string state)
        {
            // 1. State validate karo — userId lo
            var userId = await _repo.ValidateAndConsumeStateAsync(state);

            if (userId is null)
                throw new UnauthorizedAccessException(
                    "Invalid ya expired state — dobara try karein");

            // 2. GitHub se access token lo
            var accessToken = await ExchangeCodeForTokenAsync(code);

            // 3. GitHub user info lo
            var (githubId, githubLogin, avatarUrl) =
                await GetGitHubUserInfoAsync(accessToken);

            // 4. Duplicate check
            var alreadyLinked = await _repo.IsGitHubAlreadyLinkedAsync(githubId, userId.Value);
            if (alreadyLinked)
                throw new InvalidOperationException(
                    "Yeh GitHub account kisi doosre account se already connected hai");

            // 5. User update karo
            await _repo.UpdateUserGitHubInfoAsync(
                userId.Value, githubId, githubLogin, accessToken, avatarUrl);

            _logger.LogInformation("GitHub connected — UserId: {Id} → {Login}",
                userId, githubLogin);

            return new GitHubConnectResult
            {
                Success = true,
                Message = $"GitHub '{githubLogin}' connect ho gaya",
                GitHubLogin = githubLogin,
                RedirectUrl = $"{_frontendUrl}"
            };
        }


        public async Task<GitHubConnectionStatus> GetConnectionStatusAsync(int userId)
        {
            return await _repo.GetConnectionStatusAsync(userId);
        }


        public async Task DisconnectAsync(int userId)
        {
            await _repo.DisconnectAsync(userId);
        }

        private async Task<string> ExchangeCodeForTokenAsync(string code)
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Accept
                  .Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var response = await client.PostAsync(
                "https://github.com/login/oauth/access_token",
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["client_id"] = _clientId,
                    ["client_secret"] = _clientSecret,
                    ["code"] = code,
                    ["redirect_uri"] = _redirectUri
                }));

            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<JsonElement>(json);

            if (result.TryGetProperty("error", out var error))
                throw new InvalidOperationException(
                    $"GitHub token exchange failed: {error.GetString()}");

            var token = result.GetProperty("access_token").GetString();

            if (string.IsNullOrEmpty(token))
                throw new InvalidOperationException("GitHub access token nahi mila");

            return token;
        }

        private async Task<(long Id, string Login, string? AvatarUrl)>
            GetGitHubUserInfoAsync(string accessToken)
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", accessToken);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("AICodeReviewer");

            var response = await client.GetAsync("https://api.github.com/user");
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var doc = JsonSerializer.Deserialize<JsonElement>(json);

            return (
                Id: doc.GetProperty("id").GetInt64(),
                Login: doc.GetProperty("login").GetString() ?? "",
                AvatarUrl: doc.TryGetProperty("avatar_url", out var av) ? av.GetString() : null
            );
        }

        public async Task ProcessPrEventAsync(string payload)
        {
            var pullRequest = await _repo.ProcessPrEventAsync(payload);
            if (pullRequest is null) return;
            if (pullRequest.Repository?.AutoReview != true) return;

            var prId = pullRequest.Id;
            // v1 wrote "Reviewing" here, a status nothing else understands (UI showed "Unknown")
            await _repo.UpdateReviewStatusAsync(prId, "Queued");
            BackgroundJob.Enqueue<IAiReviewJob>(job => job.ExecuteAsync(prId, null));   // new 2nd arg = PerformContext

            _logger.LogInformation("AI Review job enqueued - PR #{PrNumber}", pullRequest.Number);
        }
        public bool VerifySignature(string payload, string signature, string secret)
        {
            return _repo.VerifySignature(payload, signature, secret);
        }
        }
}
