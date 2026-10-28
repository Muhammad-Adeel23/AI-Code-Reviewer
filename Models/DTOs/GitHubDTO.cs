    using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Models.DTOs
{
    public class GitHubDTO
    {
        public class GitHubConnectUrlResponse
        {
            public string Url { get; set; } = string.Empty;
            public string State { get; set; } = string.Empty;
        }

        public class GitHubConnectResult
        {
            public bool Success { get; set; }
            public string Message { get; set; } = string.Empty;
            public string GitHubLogin { get; set; } = string.Empty;
            public string RedirectUrl { get; set; } = string.Empty;
        }

        public class GitHubConnectionStatus
        {
            public bool IsConnected { get; set; }
            public string? GitHubLogin { get; set; }
            public string? AvatarUrl { get; set; }
        }
        public class GitHubRepoDto
        {
            public long GitHubId { get; set; }
            public string Name { get; set; } = string.Empty;
            public string FullName { get; set; } = string.Empty;
            public string? Description { get; set; }
            public string? Language { get; set; }
            public bool IsPrivate { get; set; }
            public string DefaultBranch { get; set; } = "main";
            public string HtmlUrl { get; set; } = string.Empty;
            public bool AlreadyImported { get; set; }
        }

        public class ImportRepositoryRequest
        {
            public long GitHubRepoId { get; set; }
            public string Name { get; set; } = string.Empty;
            public string FullName { get; set; } = string.Empty;
            public string? Language { get; set; }
            public string DefaultBranch { get; set; } = "main";
            public bool IsPrivate { get; set; }
            public string? HtmlUrl { get; set; }
        }

        public class ImportRepositoryResponse
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public string FullName { get; set; } = string.Empty;
            public bool WebhookRegistered { get; set; }
            public int PrsSynced { get; set; }
            public string Message { get; set; } = string.Empty;
        }

        public class RepositoryDto
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public string FullName { get; set; } = string.Empty;
            public string? Language { get; set; }
            public bool IsActive { get; set; }
            public bool WebhookActive { get; set; }
            public int OpenPrCount { get; set; }
            public DateTime? LastSyncedAt { get; set; }
        }
    }
}
