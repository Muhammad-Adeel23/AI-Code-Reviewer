using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Services.Rag
{
    public record VectorPoint(string Id, float[] Vector, Dictionary<string, object?> Payload);

    public record ScoredChunk(
        string FilePath, int StartLine, int EndLine, string Content,
        IReadOnlyList<string> Symbols, double Score, string Kind /* "symbol" | "semantic" */);
    public interface IVectorStore
    {
        Task EnsureCollectionAsync(CancellationToken ct = default);
        Task UpsertAsync(IReadOnlyList<VectorPoint> points, CancellationToken ct = default);
        Task DeleteByFilesAsync(int repoId, IReadOnlyCollection<string> filePaths, CancellationToken ct = default);
        Task DeleteByRepoAsync(int repoId, CancellationToken ct = default);
        Task<List<ScoredChunk>> SearchAsync(int repoId, float[] vector, int limit, double minScore, CancellationToken ct = default);
        Task<List<ScoredChunk>> FindBySymbolsAsync(int repoId, IReadOnlyCollection<string> symbols, int limit, CancellationToken ct = default);
    }
}
