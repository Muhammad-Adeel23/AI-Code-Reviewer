using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Services.Rag
{
    public class RagOptions
    {
        public bool Enabled { get; set; } = true;

        // Qdrant (Cloud free tier works). Use the REST endpoint, e.g. https://xxxx.cloud.qdrant.io:6333
        public string QdrantUrl { get; set; } = "";
        public string QdrantApiKey { get; set; } = "";
        public string Collection { get; set; } = "code_chunks";

        // Chunking / indexing limits
        public int ChunkMaxChars { get; set; } = 1800;
        public int ChunkMinChars { get; set; } = 500;
        public int ChunkOverlapLines { get; set; } = 4;
        public int MaxFileBytes { get; set; } = 100_000;
        public int MaxFilesPerIndex { get; set; } = 1500;
        public long MaxTotalBytes { get; set; } = 40_000_000;
        public int EmbedDelayMs { get; set; } = 250;      // pause between embedding batches (free-tier rate limits)

        // Retrieval
        public int TopKPerFile { get; set; } = 4;
        public int MaxQueryFiles { get; set; } = 8;
        public int MaxSymbolLookups { get; set; } = 30;
        public int MaxSymbolChunks { get; set; } = 10;
        public int MaxContextChars { get; set; } = 6000;
        public double MinScore { get; set; } = 0.5;        // cosine similarity; tune with the eval endpoint
    }

    /// <summary>Bound from appsettings section "Embedding".</summary>
    public class EmbeddingOptions
    {
        /// <summary>"Gemini" (free tier available) or "OpenAI" (also any OpenAI-compatible server via BaseUrl).</summary>
        public string Provider { get; set; } = "Gemini";
        public string ApiKey { get; set; } = "";
        public string BaseUrl { get; set; } = "";          // empty = provider default
        public string Model { get; set; } = "";            // empty = provider default
        /// <summary>Vector size. MUST match the Qdrant collection. Changing it later means re-creating the collection.</summary>
        public int Dimensions { get; set; } = 768;
        public int BatchSize { get; set; } = 32;
    }

}
