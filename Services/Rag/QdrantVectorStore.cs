using Microsoft.AspNetCore.Builder.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Services.Rag
{
    public class QdrantVectorStore : IVectorStore
    {
        private static volatile bool _collectionReady;
        private static readonly SemaphoreSlim EnsureLock = new(1, 1);

        private readonly IHttpClientFactory _http;
        private readonly RagOptions _rag;
        private readonly EmbeddingOptions _emb;
        private readonly ILogger<QdrantVectorStore> _log;

        public QdrantVectorStore(IHttpClientFactory http, IOptions<RagOptions> rag,
            IOptions<EmbeddingOptions> emb, ILogger<QdrantVectorStore> log)
        {
            _http = http;
            _rag = rag.Value;
            _emb = emb.Value;
            _log = log;
            // no throwing in the constructor (see EmbeddingService): checked on first use instead
        }

        /// <summary>Deterministic point id so re-indexing the same chunk overwrites instead of duplicating.</summary>
        public static string PointId(int repoId, string path, int chunkIndex)
        {
            var hash = MD5.HashData(Encoding.UTF8.GetBytes($"{repoId}|{path}|{chunkIndex}"));
            return new Guid(hash).ToString();
        }

        private string Url(string path) => $"{_rag.QdrantUrl.TrimEnd('/')}{path}";
        private string Col => Uri.EscapeDataString(_rag.Collection);

        private async Task<(int Status, string Body)> CallAsync(HttpMethod method, string path, object? body, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(_rag.QdrantUrl))
                throw new InvalidOperationException("Rag:QdrantUrl is not configured.");

            var json = body is null ? null : JsonSerializer.Serialize(body);
            var (status, resp) = await HttpRetry.SendAsync(_http, () =>
            {
                var req = new HttpRequestMessage(method, Url(path));
                if (!string.IsNullOrEmpty(_rag.QdrantApiKey)) req.Headers.Add("api-key", _rag.QdrantApiKey);
                if (json != null) req.Content = new StringContent(json, Encoding.UTF8, "application/json");
                return req;
            }, _log, ct);
            return ((int)status, resp);
        }

        private static void EnsureOk((int Status, string Body) r, string what)
        {
            if (r.Status >= 400)
                throw new InvalidOperationException($"Qdrant {what} failed {r.Status}: {HttpRetry.Truncate(r.Body, 300)}");
        }

        public async Task EnsureCollectionAsync(CancellationToken ct = default)
        {
            if (_collectionReady) return;
            await EnsureLock.WaitAsync(ct);
            try
            {
                if (_collectionReady) return;

                var get = await CallAsync(HttpMethod.Get, $"/collections/{Col}", null, ct);
                if (get.Status == 404)
                {
                    var create = await CallAsync(HttpMethod.Put, $"/collections/{Col}", new
                    {
                        vectors = new { size = _emb.Dimensions, distance = "Cosine" }
                    }, ct);
                    EnsureOk(create, "create collection");

                    foreach (var (field, schema) in new[] { ("repo_id", "integer"), ("file_path", "keyword"), ("symbols", "keyword") })
                    {
                        var idx = await CallAsync(HttpMethod.Put, $"/collections/{Col}/index?wait=true",
                            new { field_name = field, field_schema = schema }, ct);
                        EnsureOk(idx, $"create index {field}");
                    }
                    _log.LogInformation("Qdrant collection '{C}' created (dim {D})", _rag.Collection, _emb.Dimensions);
                }
                else
                {
                    EnsureOk(get, "get collection");
                }
                _collectionReady = true;
            }
            finally { EnsureLock.Release(); }
        }

        public async Task UpsertAsync(IReadOnlyList<VectorPoint> points, CancellationToken ct = default)
        {
            if (points.Count == 0) return;
            var body = new
            {
                points = points.Select(p => new { id = p.Id, vector = p.Vector, payload = p.Payload }).ToList()
            };
            EnsureOk(await CallAsync(HttpMethod.Put, $"/collections/{Col}/points?wait=true", body, ct), "upsert");
        }

        public async Task DeleteByFilesAsync(int repoId, IReadOnlyCollection<string> filePaths, CancellationToken ct = default)
        {
            foreach (var batch in filePaths.Chunk(100))
            {
                var filter = new
                {
                    must = new object[]
                    {
                    new { key = "repo_id", match = new { value = repoId } },
                    new { key = "file_path", match = new { any = batch } }
                    }
                };
                EnsureOk(await CallAsync(HttpMethod.Post, $"/collections/{Col}/points/delete?wait=true", new { filter }, ct), "delete by files");
            }
        }

        public async Task DeleteByRepoAsync(int repoId, CancellationToken ct = default)
        {
            var filter = new { must = new object[] { new { key = "repo_id", match = new { value = repoId } } } };
            EnsureOk(await CallAsync(HttpMethod.Post, $"/collections/{Col}/points/delete?wait=true", new { filter }, ct), "delete by repo");
        }

        public async Task<List<ScoredChunk>> SearchAsync(int repoId, float[] vector, int limit, double minScore, CancellationToken ct = default)
        {
            var body = new
            {
                vector,
                limit,
                with_payload = true,
                score_threshold = minScore,
                filter = new { must = new object[] { new { key = "repo_id", match = new { value = repoId } } } }
            };
            var r = await CallAsync(HttpMethod.Post, $"/collections/{Col}/points/search", body, ct);
            EnsureOk(r, "search");

            using var doc = JsonDocument.Parse(r.Body);
            var list = new List<ScoredChunk>();
            foreach (var hit in doc.RootElement.GetProperty("result").EnumerateArray())
                list.Add(ToChunk(hit.GetProperty("payload"), hit.GetProperty("score").GetDouble(), "semantic"));
            return list;
        }

        public async Task<List<ScoredChunk>> FindBySymbolsAsync(int repoId, IReadOnlyCollection<string> symbols, int limit, CancellationToken ct = default)
        {
            if (symbols.Count == 0) return new();

            var body = new
            {
                limit,
                with_payload = true,
                with_vector = false,
                filter = new
                {
                    must = new object[]
                    {
                    new { key = "repo_id", match = new { value = repoId } },
                    new { key = "symbols", match = new { any = symbols } }
                    }
                }
            };
            var r = await CallAsync(HttpMethod.Post, $"/collections/{Col}/points/scroll", body, ct);
            EnsureOk(r, "scroll");

            using var doc = JsonDocument.Parse(r.Body);
            var list = new List<ScoredChunk>();
            foreach (var p in doc.RootElement.GetProperty("result").GetProperty("points").EnumerateArray())
                list.Add(ToChunk(p.GetProperty("payload"), 1.0, "symbol"));
            return list;
        }

        private static ScoredChunk ToChunk(JsonElement payload, double score, string kind)
        {
            var symbols = new List<string>();
            if (payload.TryGetProperty("symbols", out var s) && s.ValueKind == JsonValueKind.Array)
                symbols.AddRange(s.EnumerateArray().Select(x => x.GetString() ?? "").Where(x => x.Length > 0));

            return new ScoredChunk(
                payload.TryGetProperty("file_path", out var fp) ? fp.GetString() ?? "" : "",
                payload.TryGetProperty("start_line", out var sl) ? sl.GetInt32() : 0,
                payload.TryGetProperty("end_line", out var el) ? el.GetInt32() : 0,
                payload.TryGetProperty("content", out var c) ? c.GetString() ?? "" : "",
                symbols, score, kind);
        }
    }

}
