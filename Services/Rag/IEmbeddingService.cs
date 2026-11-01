using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Services.Rag
{
    public interface IEmbeddingService
    {
        /// <param name="isQuery">true for search queries, false for documents (matters for Gemini task types).</param>
        Task<List<float[]>> EmbedAsync(IReadOnlyList<string> texts, bool isQuery, CancellationToken ct = default);
    }
}
