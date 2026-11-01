using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Models.Entities;
using Repository.GitHub;
using Repository.GitHubRepos;
using Services.Rag;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using static Models.DTOs.GitHubDTO;

namespace Services.GitHubRepos
{
    public class GitHubRepositoryService : IGitHubRepositoryService
    {
        private readonly IGitHubRepositoryRepository _repo;
        private readonly IGitHubConnectRepository _connectRepo;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _config;
        private readonly AppDbContext _db;
        private readonly ILogger<GitHubRepositoryService> _logger;

        private readonly string _webhookSecret;
        private readonly string _backendUrl;

        public GitHubRepositoryService(
            IGitHubRepositoryRepository repo,
            IGitHubConnectRepository connectRepo,
            AppDbContext dbContext,
            IHttpClientFactory httpClientFactory,
            IConfiguration config,
            ILogger<GitHubRepositoryService> logger)
        {
            _repo = repo;
            _connectRepo = connectRepo;
            _httpClientFactory = httpClientFactory;
            _config = config;
            _logger = logger;
            _db = dbContext;
            _webhookSecret = config["GitHub:WebhookSecret"] ?? throw new InvalidOperationException("WebhookSecret missing");
   //         _backendUrl = config["App:BackendUrl"] ?? "https://localhost:7260";
            _backendUrl = config["GitHub:BackendUrl"]!;
        }

        // ─── User ki GitHub repos fetch karo ─────────────────────────────────

        public async Task<List<GitHubRepoDto>> GetGitHubReposAsync(int userId)
        {
            var token = await _connectRepo.GetUserGitHubTokenAsync(userId);

            if (string.IsNullOrEmpty(token))
                throw new InvalidOperationException("GitHub connect nahi hai");

            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("AICodeReviewer");

            // GitHub se sari repos lo
            var response = await client.GetAsync(
                "https://api.github.com/user/repos?per_page=100&sort=updated&type=owner");
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var repos = JsonSerializer.Deserialize<JsonElement[]>(json) ?? [];

            // Jo pehle se import hain unhe mark karo
            var result = new List<GitHubRepoDto>();
            foreach (var r in repos)
            {
                var githubId = r.GetProperty("id").GetInt64();
                result.Add(new GitHubRepoDto
                {
                    GitHubId = githubId,
                    Name = r.GetProperty("name").GetString() ?? "",
                    FullName = r.GetProperty("full_name").GetString() ?? "",
                    Description = r.TryGetProperty("description", out var d) ? d.GetString() : null,
                    Language = r.TryGetProperty("language", out var l) ? l.GetString() : null,
                    IsPrivate = r.GetProperty("private").GetBoolean(),
                    DefaultBranch = r.TryGetProperty("default_branch", out var db) ? db.GetString() ?? "main" : "main",
                    HtmlUrl = r.GetProperty("html_url").GetString() ?? "",
                    AlreadyImported = await _repo.IsAlreadyImportedAsync(githubId)
                });
            }

            return result;
        }

        // ─── Repository import karo + webhook register karo ──────────────────

        public async Task<ImportRepositoryResponse> ImportRepositoryAsync(
            int userId, ImportRepositoryRequest request, int TenantId)
        {
            // Duplicate check
            if (await _repo.IsAlreadyImportedAsync(request.GitHubRepoId))
                throw new InvalidOperationException("Repository has already imported");

            var token = await _connectRepo.GetUserGitHubTokenAsync(userId);
            if (string.IsNullOrEmpty(token))
                throw new InvalidOperationException("GitHub did not connect");

            // DB mein save karo
            var repo = new Models.Entities.Repository
            {
                OwnerId = userId,
                GitHubRepoId = request.GitHubRepoId,
                Name = request.Name,
                FullName = request.FullName,
                Language = request.Language,
                DefaultBranch = request.DefaultBranch,
                IsPrivate = request.IsPrivate,
                GitHubUrl = request.HtmlUrl,
                IsActive = true,
                WebhookActive = false,
                AutoReview = true,
                SeverityThreshold = "ALL",
                MaxFilesPerReview = 20,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                OrganizationId = TenantId
            };

            var repoId = await _repo.SaveRepositoryAsync(repo);

            // Webhook register karo
            var webhookRegistered = false;
            long? webhookId = null;

            try
            {
                webhookId = await RegisterWebhookAsync(token, request.FullName);
                repo.WebhookId = webhookId;
                repo.WebhookActive = true;
                repo.WebhookSecret = _webhookSecret;
                webhookRegistered = true;

                await _repo.UpdateLastSyncedAsync(repoId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occured in Register Webhook — {Repo}", request.FullName);
            }

            // Existing open PRs sync karo
            var prsSynced = await SyncOpenPrsAsync(token, request.FullName, repoId);

            BackgroundJob.Enqueue<IIndexRepoJob>(j => j.ExecuteAsync(repoId, false));   // build the vector index

            _logger.LogInformation("Repo imported: {Repo} — Webhook: {Wh} — PRs: {Prs}",
                request.FullName, webhookRegistered, prsSynced);

            return new ImportRepositoryResponse
            {
                Id = repoId,
                Name = request.Name,
                FullName = request.FullName,
                WebhookRegistered = webhookRegistered,
                PrsSynced = prsSynced,
                Message = $"Repositories have been imported. {prsSynced} PRs synced."
            };
        }

        public async Task<List<RepositoryDto>> GetImportedReposAsync(int userId)
        {
            return await _repo.GetUserRepositoriesAsync(userId);
        }

        // ─── Private: Webhook register karo ──────────────────────────────────

        private async Task<long> RegisterWebhookAsync(string token, string repoFullName)
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("AICodeReviewer");

            var webhookUrl = $"{_backendUrl}/api/webhooks/github";

            var body = new
            {
                name = "web",
                active = true,
                events = new[] { "pull_request", "push" },
                config = new
                {
                    url = webhookUrl,
                    content_type = "json",
                    secret = _webhookSecret,
                    insecure_ssl = "0"
                }
            };

            var json = JsonSerializer.Serialize(body);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await client.PostAsync(
                $"https://api.github.com/repos/{repoFullName}/hooks", content);



            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new InvalidOperationException($"Webhook creation failed: {error}");
            }

            var resJson = await response.Content.ReadAsStringAsync();
            var doc = JsonSerializer.Deserialize<JsonElement>(resJson);

            return doc.GetProperty("id").GetInt64();
        }

        private async Task<int?> ResolveAuthorUserIdAsync(
            string? token, string repoFullName, int prNumber, int? organizationId, string authorGitHubLogin)
        {
            try
            {
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

                if (!string.IsNullOrEmpty(token))
                {
                    var client = _httpClientFactory.CreateClient();
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
                _logger.LogError(ex, "Error resolving author user for PR #{PrNumber}", prNumber);
            }

            return null;
        }

        private async Task<int> SyncOpenPrsAsync(
            string token, string repoFullName, int repoId)
        {
            try
            {
                var repo = await _db.Repositories.FindAsync(repoId);
                var client = _httpClientFactory.CreateClient();
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", token);
                client.DefaultRequestHeaders.UserAgent.ParseAdd("AICodeReviewer");

                var response = await client.GetAsync(
                    $"https://api.github.com/repos/{repoFullName}/pulls?state=open&per_page=50");

                if (!response.IsSuccessStatusCode) return 0;

                var json = await response.Content.ReadAsStringAsync();
                var prs = JsonSerializer.Deserialize<JsonElement[]>(json) ?? [];

                var count = 0;
                foreach (var pr in prs)
                {
                    var githubPrId = pr.GetProperty("id").GetInt64();
                    var prNumber = pr.GetProperty("number").GetInt32();

                    if (await _repo.PrExistsAsync(githubPrId)) continue;

                    var authorLogin = pr.GetProperty("user").GetProperty("login").GetString() ?? "";
                    var authorUserId = await ResolveAuthorUserIdAsync(token, repoFullName, prNumber, repo?.OrganizationId, authorLogin);

                    // PR save karo
                    var newPr = new Models.Entities.PullRequest
                    {
                        HeadSha = pr.GetProperty("head").GetProperty("sha").GetString(),
                        RepositoryId = repoId,
                        GitHubPrId = githubPrId,
                        Number = prNumber,
                        Title = pr.GetProperty("title").GetString() ?? "",
                        Description = pr.TryGetProperty("body", out var b)
                                            ? b.GetString() : null,
                        AuthorGitHubLogin = authorLogin,
                        AuthorAvatarUrl = pr.GetProperty("user")
                                              .TryGetProperty("avatar_url", out var av)
                                            ? av.GetString() : null,
                        AuthorUserId = authorUserId,
                        SourceBranch = pr.GetProperty("head")
                                              .GetProperty("ref").GetString() ?? "",
                        TargetBranch = pr.GetProperty("base")
                                              .GetProperty("ref").GetString() ?? "",
                        GitHubPrUrl = pr.GetProperty("html_url").GetString() ?? "",
                        Status = "Open",
                        ReviewStatus = "Pending",
                        TotalAdditions = pr.TryGetProperty("additions", out var additions)
                                            ? additions.GetInt32() : 0,
                        TotalDeletions = pr.TryGetProperty("deletions", out var deletions)
                                            ? deletions.GetInt32() : 0,
                        ChangedFilesCount = pr.TryGetProperty("changed_files", out var cf)
                                            ? cf.GetInt32() : 0,
                        GitHubCreatedAt = pr.GetProperty("created_at").GetDateTime(),
                        GitHubUpdatedAt = pr.GetProperty("updated_at").GetDateTime(),
                        CurrentReviewVersion = 0,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };

                    await _repo.SavePullRequestAsync(newPr);

                    // ─── FILES BHI FETCH KARO ─────────────────────────────────────
                    await SavePrFilesAsync(token, repoFullName, prNumber, newPr.Id);

                    count++;
                }

                return count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PR sync mein masla — {Repo}", repoFullName);
                return 0;
            }
        }

        private async Task SavePrFilesAsync(
            string token, string repoFullName, int prNumber, int pullRequestId)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", token);
                client.DefaultRequestHeaders.UserAgent.ParseAdd("AICodeReviewer");

                // GitHub se changed files fetch karo
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

                _logger.LogInformation(
                    "Saved {Count} files for PR #{Num}",
                    files.Length, prNumber);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "PR files fetch mein masla — PR#{Num}", prNumber);
            }
        }
    }
}