using Models.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Services.Rag
{
    public record RetrievedContext(string Text, int ChunkCount, int SymbolHits, int SemanticHits, List<string> Sources)
    {
        public static RetrievedContext Empty => new("", 0, 0, 0, new List<string>());
        public bool IsEmpty => ChunkCount == 0;
    }

    public interface IContextRetriever
    {
        Task<bool> IsReadyAsync(int repoId, CancellationToken ct = default);

        /// <summary>Context for reviewing a set of changed files: definitions of what the diff calls + similar code.</summary>
        Task<RetrievedContext> BuildForFilesAsync(int repoId, IReadOnlyList<PullRequestFile> files, CancellationToken ct = default);

        /// <summary>Context for "ask the codebase" questions.</summary>
        Task<RetrievedContext> BuildForQuestionAsync(int repoId, string question, int maxChunks, CancellationToken ct = default);
    }
}
