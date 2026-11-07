using Models.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Repository.Rag
{
    public record IndexTarget(int RepositoryId, string FullName, string DefaultBranch, string? GitHubToken);
    public record IndexBegin(bool WasReady, string? LastCommitSha);
    public record IndexedFileRecord(string Path, string Hash, int ChunkCount);
    public record PushTarget(int RepositoryId, string DefaultBranch, bool UseRag);
    public record RepoSettingsView(int Id, bool UseRag, bool AutoReview, string SeverityThreshold, string? CustomInstructions);
    public interface IRepoIndexRepository
    {
        Task<IndexTarget?> GetIndexTargetAsync(int repositoryId, CancellationToken ct = default);
        Task<PushTarget?> FindForPushAsync(long githubRepoId, CancellationToken ct = default);

        Task<RepoIndexState?> GetStateAsync(int repositoryId, CancellationToken ct = default);
        Task<bool> HasIndexAsync(int repositoryId, CancellationToken ct = default);
        Task EnsureStateAsync(int repositoryId, CancellationToken ct = default);

        Task<IndexBegin> BeginIndexingAsync(int repositoryId, CancellationToken ct = default);
        Task MarkUpToDateAsync(int repositoryId, CancellationToken ct = default);
        Task<Dictionary<string, string>> GetFileHashesAsync(int repositoryId, CancellationToken ct = default);
        Task CompleteIndexingAsync(int repositoryId, string commitSha,
            IReadOnlyCollection<IndexedFileRecord> upserted, IReadOnlyCollection<string> deleted, CancellationToken ct = default);
        Task FailIndexingAsync(int repositoryId, string error);
    }
    public interface IRepoSettingsRepository
    {
        Task<RepoSettingsView?> GetAsync(int organizationId, int repositoryId);
        Task UpdateAsync(int organizationId, int repositoryId, string? customInstructions,
            bool? useRag, bool? autoReview, string? severityThreshold);
    }
}
