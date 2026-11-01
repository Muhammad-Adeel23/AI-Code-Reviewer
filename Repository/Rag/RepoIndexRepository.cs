using Models.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Repository.Rag
{
    public class RepoIndexRepository : IRepoIndexRepository
    {
        private readonly AppDbContext _db;
        public RepoIndexRepository(AppDbContext db) => _db = db;

        public Task<IndexTarget?> GetIndexTargetAsync(int repositoryId, CancellationToken ct = default) =>
            _db.Repositories.AsNoTracking()
                .Where(r => r.Id == repositoryId)
                .Select(r => new IndexTarget(r.Id, r.FullName, r.DefaultBranch, r.Owner.GitHubAccessToken))
                .FirstOrDefaultAsync(ct);

        public Task<PushTarget?> FindForPushAsync(long githubRepoId, CancellationToken ct = default) =>
            _db.Repositories.AsNoTracking()
                .Where(r => r.GitHubRepoId == githubRepoId && r.IsActive)
                .Select(r => new PushTarget(r.Id, r.DefaultBranch, r.UseRag))
                .FirstOrDefaultAsync(ct);

        public Task<RepoIndexState?> GetStateAsync(int repositoryId, CancellationToken ct = default) =>
            _db.RepoIndexStates.AsNoTracking().FirstOrDefaultAsync(s => s.RepositoryId == repositoryId, ct);

        // "has been indexed successfully at least once" - retrieval may continue while a re-index is running
        public Task<bool> HasIndexAsync(int repositoryId, CancellationToken ct = default) =>
            _db.RepoIndexStates.AnyAsync(s => s.RepositoryId == repositoryId && s.LastIndexedAt != null, ct);

        public async Task EnsureStateAsync(int repositoryId, CancellationToken ct = default)
        {
            if (await _db.RepoIndexStates.AnyAsync(s => s.RepositoryId == repositoryId, ct)) return;
            _db.RepoIndexStates.Add(new RepoIndexState { RepositoryId = repositoryId, Status = "Pending" });
            await _db.SaveChangesAsync(ct);
        }

        public async Task<IndexBegin> BeginIndexingAsync(int repositoryId, CancellationToken ct = default)
        {
            var state = await _db.RepoIndexStates.FirstOrDefaultAsync(s => s.RepositoryId == repositoryId, ct);
            if (state is null)
            {
                state = new RepoIndexState { RepositoryId = repositoryId };
                _db.RepoIndexStates.Add(state);
            }

            var begin = new IndexBegin(state.Status == "Ready", state.LastCommitSha);
            state.Status = "Indexing";
            state.StartedAt = DateTime.UtcNow;
            state.LastError = null;
            state.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            return begin;
        }

        public async Task MarkUpToDateAsync(int repositoryId, CancellationToken ct = default)
        {
            var state = await _db.RepoIndexStates.FirstAsync(s => s.RepositoryId == repositoryId, ct);
            state.Status = "Ready";
            state.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }

        public Task<Dictionary<string, string>> GetFileHashesAsync(int repositoryId, CancellationToken ct = default) =>
            _db.RepoIndexedFiles.AsNoTracking()
                .Where(f => f.RepositoryId == repositoryId)
                .ToDictionaryAsync(f => f.FilePath, f => f.ContentHash, StringComparer.Ordinal, ct);

        public async Task CompleteIndexingAsync(int repositoryId, string commitSha,
            IReadOnlyCollection<IndexedFileRecord> upserted, IReadOnlyCollection<string> deleted, CancellationToken ct = default)
        {
            var rows = await _db.RepoIndexedFiles.Where(f => f.RepositoryId == repositoryId).ToListAsync(ct);
            var byPath = rows.ToDictionary(r => r.FilePath, StringComparer.Ordinal);

            foreach (var path in deleted)
                if (byPath.TryGetValue(path, out var gone)) _db.RepoIndexedFiles.Remove(gone);

            foreach (var f in upserted)
            {
                if (byPath.TryGetValue(f.Path, out var row))
                {
                    row.ContentHash = f.Hash;
                    row.ChunkCount = f.ChunkCount;
                    row.IndexedAt = DateTime.UtcNow;
                }
                else
                {
                    _db.RepoIndexedFiles.Add(new RepoIndexedFile
                    {
                        RepositoryId = repositoryId,
                        FilePath = f.Path,
                        ContentHash = f.Hash,
                        ChunkCount = f.ChunkCount
                    });
                }
            }
            await _db.SaveChangesAsync(ct);

            var state = await _db.RepoIndexStates.FirstAsync(s => s.RepositoryId == repositoryId, ct);
            state.FilesIndexed = await _db.RepoIndexedFiles.CountAsync(f => f.RepositoryId == repositoryId, ct);
            state.ChunksIndexed = await _db.RepoIndexedFiles.Where(f => f.RepositoryId == repositoryId)
                .SumAsync(f => (int?)f.ChunkCount, ct) ?? 0;
            state.Status = "Ready";
            state.LastCommitSha = commitSha;
            state.LastIndexedAt = DateTime.UtcNow;
            state.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }

        public async Task FailIndexingAsync(int repositoryId, string error)
        {
            var state = await _db.RepoIndexStates.FirstOrDefaultAsync(s => s.RepositoryId == repositoryId);
            if (state is null) return;
            state.Status = "Failed";
            state.LastError = error.Length > 480 ? error[..480] : error;
            state.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }
    }

    public class RepoSettingsRepository : IRepoSettingsRepository
    {
        private readonly AppDbContext _db;
        public RepoSettingsRepository(AppDbContext db) => _db = db;

        public Task<RepoSettingsView?> GetAsync(int organizationId, int repositoryId) =>
            _db.Repositories.AsNoTracking()
                .Where(r => r.Id == repositoryId && r.IsActive && r.OrganizationId == organizationId)
                .Select(r => new RepoSettingsView(r.Id, r.UseRag, r.AutoReview, r.SeverityThreshold, r.CustomInstructions))
                .FirstOrDefaultAsync();

        public async Task UpdateAsync(int organizationId, int repositoryId, string? customInstructions,
            bool? useRag, bool? autoReview, string? severityThreshold)
        {
            var repo = await _db.Repositories.FirstOrDefaultAsync(r =>
                r.Id == repositoryId && r.IsActive && r.OrganizationId == organizationId)
                ?? throw new KeyNotFoundException("Repository nahi mili");

            if (customInstructions != null) repo.CustomInstructions = customInstructions;
            if (useRag.HasValue) repo.UseRag = useRag.Value;
            if (autoReview.HasValue) repo.AutoReview = autoReview.Value;
            if (severityThreshold != null) repo.SeverityThreshold = severityThreshold;
            repo.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }
    }

}
