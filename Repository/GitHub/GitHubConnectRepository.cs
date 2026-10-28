using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Models.Entities;
using Repository.AIReview;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using static Models.DTOs.GitHubDTO;

namespace Repository.GitHub
{
    public class GitHubConnectRepository : IGitHubConnectRepository
    {
        private readonly AppDbContext _db;
        private readonly IConfiguration _config;
        private readonly ILogger<GitHubConnectRepository> _logger;
        private readonly string _frontendUrl;

        public GitHubConnectRepository(
            AppDbContext db,
            IConfiguration config,
            ILogger<GitHubConnectRepository> logger)
        {
            _db = db;
            _config = config;
            _logger = logger;
            _frontendUrl = config["App:FrontendUrl"] ?? "http://localhost:3000";
        }

        public async Task<string> SaveOAuthStateAsync(int userId)
        {
            var state = Guid.NewGuid().ToString("N");

            _db.OauthStates.Add(new OauthState
            {
                State = state,
                Provider = "GitHubConnect",
                UserId = userId,
                ExpiresAt = DateTime.UtcNow.AddMinutes(10),
                IsUsed = false,
                CreatedAt = DateTime.UtcNow
            });

            await _db.SaveChangesAsync();
            return state;
        }

        // ─── State validate karo aur consume karo ────────────────────────────

        public async Task<int?> ValidateAndConsumeStateAsync(string state)
        {
            var oauthState = await _db.OauthStates
                .FirstOrDefaultAsync(s => s.State == state
                                       && s.Provider == "GitHubConnect"
                                       && !s.IsUsed
                                       && s.ExpiresAt > DateTime.UtcNow);

            if (oauthState is null)
                return null;

            // Consume karo — dubara use na ho sake
            oauthState.IsUsed = true;
            await _db.SaveChangesAsync();

            return oauthState.UserId;
        }

        // ─── GitHub account kisi aur user se linked check karo ───────────────

        public async Task<bool> IsGitHubAlreadyLinkedAsync(long githubId, int currentUserId)
        {
            return await _db.Users
                .AnyAsync(u => u.GitHubId == githubId
                            && u.Id != currentUserId);
        }

        // ─── User ka GitHub info update karo ─────────────────────────────────

        public async Task UpdateUserGitHubInfoAsync(
            int userId,
            long githubId,
            string githubLogin,
            string accessToken,
            string? avatarUrl)
        {
            var user = await _db.Users.FindAsync(userId)
                       ?? throw new KeyNotFoundException("User nahi mila");

            user.GitHubId = githubId;
            user.GitHubLogin = githubLogin;
            user.GitHubAccessToken = accessToken;

            if (string.IsNullOrEmpty(user.AvatarUrl) && !string.IsNullOrEmpty(avatarUrl))
                user.AvatarUrl = avatarUrl;

            user.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            _logger.LogInformation("GitHub info updated — UserId: {Id} → {Login}",
                userId, githubLogin);
        }

        // ─── Connection status check karo ────────────────────────────────────

        public async Task<GitHubConnectionStatus> GetConnectionStatusAsync(int userId)
        {
            var user = await _db.Users
                .Where(u => u.Id == userId)
                .Select(u => new
                {
                    u.GitHubLogin,
                    u.GitHubAccessToken,
                    u.AvatarUrl
                })
                .FirstOrDefaultAsync()
                ?? throw new KeyNotFoundException("User nahi mila");

            return new GitHubConnectionStatus
            {
                IsConnected = !string.IsNullOrEmpty(user.GitHubAccessToken),
                GitHubLogin = user.GitHubLogin,
                AvatarUrl = user.AvatarUrl
            };
        }

        // ─── GitHub disconnect karo ───────────────────────────────────────────

        public async Task DisconnectAsync(int userId)
        {
            var user = await _db.Users.FindAsync(userId)
                       ?? throw new KeyNotFoundException("User nahi mila");

            user.GitHubAccessToken = null;
            user.GitHubId = null;
            user.GitHubLogin = null;
            user.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            _logger.LogInformation("GitHub disconnected — UserId: {Id}", userId);
        }

        // ─── User ka GitHub access token lo ──────────────────────────────────

        public async Task<string?> GetUserGitHubTokenAsync(int userId)
        {
            return await _db.Users
                .Where(u => u.Id == userId)
                .Select(u => u.GitHubAccessToken)
                .FirstOrDefaultAsync();
        }

        public async Task<Models.Entities.PullRequest?> ProcessPrEventAsync(string payload)
        {
            try
            {
                using var doc = JsonDocument.Parse(payload);
                var root = doc.RootElement;

                var action = root.GetProperty("action").GetString();

                _logger.LogInformation(
                    "PR webhook — Action: {Action}",
                    action);

                switch (action)
                {
                    case "opened":
                    case "reopened":
                        return await HandlePrOpenedAsync(root);

                    case "synchronize":
                        return await HandlePrSynchronizedAsync(root);

                    case "closed":
                        await HandlePrClosedAsync(root);
                        return null;

                    default:
                        return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "PR event processing mein masla");

                return null;
            }
        }

        private async Task<int?> ResolveAuthorUserIdAsync(
            string? token, string repoFullName, int prNumber, int? organizationId, string authorGitHubLogin)
        {
            try
            {
                // 1. Agar user ne pehle se GitHub Login link kiya hua hai
                if (!string.IsNullOrEmpty(authorGitHubLogin))
                {
                    var userQuery = _db.Users.AsQueryable();
                    if (organizationId.HasValue)
                    {
                        userQuery = userQuery.Where(u => u.OrganizationMembers.Any(m => m.OrganizationId == organizationId.Value && m.Status == "Active"));
                    }

                    var userByLogin = await userQuery.FirstOrDefaultAsync(u => u.GitHubLogin != null && u.GitHubLogin.ToLower() == authorGitHubLogin.ToLower());
                    if (userByLogin != null) return userByLogin.Id;
                }

                // 2. Agar token available hai, GitHub PR commits se author email nikalo
                if (!string.IsNullOrEmpty(token))
                {
                    using var client = new HttpClient();
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                    client.DefaultRequestHeaders.UserAgent.ParseAdd("AICodeReviewer");

                    var response = await client.GetAsync($"https://api.github.com/repos/{repoFullName}/pulls/{prNumber}/commits?per_page=10");
                    if (response.IsSuccessStatusCode)
                    {
                        var json = await response.Content.ReadAsStringAsync();
                        var commits = JsonSerializer.Deserialize<JsonElement[]>(json) ?? [];

                        var emails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        foreach (var commit in commits)
                        {
                            if (commit.TryGetProperty("commit", out var commitObj) &&
                                commitObj.TryGetProperty("author", out var authorObj) &&
                                authorObj.TryGetProperty("email", out var emailProp))
                            {
                                var email = emailProp.GetString();
                                if (!string.IsNullOrWhiteSpace(email))
                                {
                                    emails.Add(email.Trim().ToLower());
                                }
                            }
                        }

                        if (emails.Count > 0)
                        {
                            var userQuery = _db.Users.AsQueryable();
                            if (organizationId.HasValue)
                            {
                                userQuery = userQuery.Where(u => u.OrganizationMembers.Any(m => m.OrganizationId == organizationId.Value && m.Status == "Active"));
                            }

                            var userByEmail = await userQuery.FirstOrDefaultAsync(u => emails.Contains(u.Email.ToLower()));
                            if (userByEmail != null)
                            {
                                if (string.IsNullOrEmpty(userByEmail.GitHubLogin) && !string.IsNullOrEmpty(authorGitHubLogin))
                                {
                                    userByEmail.GitHubLogin = authorGitHubLogin;
                                    await _db.SaveChangesAsync();
                                }
                                return userByEmail.Id;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resolving PR author user for PR #{PrNumber}", prNumber);
            }

            return null;
        }

        private async Task<Models.Entities.PullRequest?> HandlePrOpenedAsync(JsonElement root)
        {
            var pr = root.GetProperty("pull_request");

            var githubRepoId = root
                .GetProperty("repository")
                .GetProperty("id")
                .GetInt64();

            var githubPrId = pr
                .GetProperty("id")
                .GetInt64();

            var repo = await _db.Repositories
                .FirstOrDefaultAsync(
                    r => r.GitHubRepoId == githubRepoId &&
                         r.IsActive);

            if (repo is null)
            {
                _logger.LogWarning(
                    "Webhook: Repo not found — GitHubRepoId: {Id}",
                    githubRepoId);

                return null;
            }

            if (await _db.PullRequests
                .AnyAsync(p => p.GitHubPrId == githubPrId))
            {
                _logger.LogInformation(
                    "PR already exists — GitHubPrId: {Id}",
                    githubPrId);

                return null;
            }

            var owner = await _db.Users.FindAsync(repo.OwnerId);
            var token = owner?.GitHubAccessToken;
            var prNumber = pr.GetProperty("number").GetInt32();
            var authorLogin = pr.GetProperty("user").GetProperty("login").GetString() ?? "";

            var authorUserId = await ResolveAuthorUserIdAsync(
                token,
                repo.FullName,
                prNumber,
                repo.OrganizationId,
                authorLogin);

            var newPr = new Models.Entities.PullRequest
            {
                RepositoryId = repo.Id,
                GitHubPrId = githubPrId,
                Number = prNumber,
                Title = pr.GetProperty("title").GetString() ?? "",
                Description = pr.TryGetProperty("body", out var body) ? body.GetString() : null,
                AuthorGitHubLogin = authorLogin,
                AuthorAvatarUrl = pr.GetProperty("user").TryGetProperty("avatar_url", out var av) ? av.GetString() : null,
                AuthorUserId = authorUserId,
                SourceBranch = pr.GetProperty("head").GetProperty("ref").GetString() ?? "",
                TargetBranch = pr.GetProperty("base").GetProperty("ref").GetString() ?? "",
                GitHubPrUrl = pr.GetProperty("html_url").GetString() ?? "",
                Status = "Open",
                ReviewStatus = "Pending",
                TotalAdditions = pr.TryGetProperty("additions", out var add) ? add.GetInt32() : 0,
                TotalDeletions = pr.TryGetProperty("deletions", out var del) ? del.GetInt32() : 0,
                ChangedFilesCount = pr.TryGetProperty("changed_files", out var cf) ? cf.GetInt32() : 0,
                GitHubCreatedAt = pr.GetProperty("created_at").GetDateTime(),
                GitHubUpdatedAt = pr.GetProperty("updated_at").GetDateTime(),
                CurrentReviewVersion = 0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _db.PullRequests.Add(newPr);
            await _db.SaveChangesAsync();

            _logger.LogInformation(
                "PR #{Num} saved — Repo: {Repo} — AuthorUserId: {AuthorUserId}",
                newPr.Number,
                repo.FullName,
                newPr.AuthorUserId);

            // Fetch PR files
            if (!string.IsNullOrEmpty(token))
            {
                await FetchAndSavePrFilesAsync(
                    token,
                    repo.FullName,
                    newPr.Number,
                    newPr.Id);
            }

            return newPr;
        }

        // ─── PR Synchronized (new commits) ───────────────────────────────────
        private async Task FetchAndSavePrFilesAsync(
            string token, string repoFullName, int prNumber, int pullRequestId)
        {
            try
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", token);
                client.DefaultRequestHeaders.UserAgent.ParseAdd("AICodeReviewer");

                var response = await client.GetAsync(
                    $"https://api.github.com/repos/{repoFullName}/pulls/{prNumber}/files?per_page=100");

                if (!response.IsSuccessStatusCode) return;

                var json = await response.Content.ReadAsStringAsync();
                var files = JsonSerializer.Deserialize<JsonElement[]>(json) ?? [];

                foreach (var file in files)
                {
                    _db.PullRequestFiles.Add(new PullRequestFile
                    {
                        PullRequestId = pullRequestId,
                        FileName = file.GetProperty("filename").GetString() ?? "",
                        Status = file.GetProperty("status").GetString() ?? "",
                        Additions = file.TryGetProperty("additions", out var add)
                                        ? add.GetInt32() : 0,
                        Deletions = file.TryGetProperty("deletions", out var del)
                                        ? del.GetInt32() : 0,
                        Patch = file.TryGetProperty("patch", out var patch)
                                        ? patch.GetString() : null
                    });
                }

                await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Files fetch mein masla PR#{Num}", prNumber);
            }
        }

        private async Task<Models.Entities.PullRequest?> HandlePrSynchronizedAsync(
            JsonElement root)
        {
            var githubPrId = root
                .GetProperty("pull_request")
                .GetProperty("id")
                .GetInt64();

            var pr = await _db.PullRequests
                .Include(p => p.Repository)
                .FirstOrDefaultAsync(
                    p => p.GitHubPrId == githubPrId);

            if (pr is null)
            {
                return await HandlePrOpenedAsync(root);
            }

            pr.ReviewStatus = "Pending";
            pr.GitHubUpdatedAt = DateTime.UtcNow;
            pr.UpdatedAt = DateTime.UtcNow;

            var owner = await _db.Users.FindAsync(pr.Repository.OwnerId);

            // Agar AuthorUserId null tha, toh ab resolve kar lo
            if (pr.AuthorUserId == null)
            {
                pr.AuthorUserId = await ResolveAuthorUserIdAsync(
                    owner?.GitHubAccessToken,
                    pr.Repository.FullName,
                    pr.Number,
                    pr.Repository.OrganizationId,
                    pr.AuthorGitHubLogin);
            }

            await _db.SaveChangesAsync();

            _logger.LogInformation(
                "PR #{Num} synchronized",
                pr.Number);

            if (owner?.GitHubAccessToken != null)
            {
                // Delete old files
                var oldFiles = await _db.PullRequestFiles
                    .Where(f => f.PullRequestId == pr.Id)
                    .ToListAsync();

                if (oldFiles.Any())
                {
                    _db.PullRequestFiles.RemoveRange(oldFiles);
                    await _db.SaveChangesAsync();
                }

                await FetchAndSavePrFilesAsync(
                    owner.GitHubAccessToken,
                    pr.Repository.FullName,
                    pr.Number,
                    pr.Id);
            }

            return pr;
        }

        private async Task HandlePrClosedAsync(JsonElement root)
        {
            var prData = root.GetProperty("pull_request");
            var githubPrId = prData.GetProperty("id").GetInt64();
            var merged = prData.GetProperty("merged").GetBoolean();

            var pr = await _db.PullRequests
                .FirstOrDefaultAsync(p => p.GitHubPrId == githubPrId);

            if (pr is null) return;

            pr.Status = merged ? "Merged" : "Closed";
            pr.UpdatedAt = DateTime.UtcNow;

            if (merged && prData.TryGetProperty("merged_at", out var mergedAt))
                pr.MergedAt = mergedAt.GetDateTime();

            await _db.SaveChangesAsync();

            _logger.LogInformation("PR #{Num} {Status}", pr.Number, pr.Status);
        }

        // ─── Signature verify karo ────────────────────────────────────────────

        public bool VerifySignature(string payload, string signature, string secret)
        {
            if (string.IsNullOrEmpty(signature) || !signature.StartsWith("sha256="))
                return false;

            var receivedHash = signature["sha256=".Length..];
            var secretBytes = Encoding.UTF8.GetBytes(secret);
            var payloadBytes = Encoding.UTF8.GetBytes(payload);

            using var hmac = new HMACSHA256(secretBytes);
            var computedHash = Convert.ToHexString(hmac.ComputeHash(payloadBytes)).ToLower();

            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(computedHash),
                Encoding.UTF8.GetBytes(receivedHash));
        }

        public async Task UpdateReviewStatusAsync(
            int pullRequestId,
            string status)
        {
            var pr = await _db.PullRequests
                .FirstOrDefaultAsync(p => p.Id == pullRequestId);

            if (pr is null)
                return;

            pr.ReviewStatus = status;
            pr.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
        }
    }
}