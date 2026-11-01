using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Services.Rag
{
    public class EmbeddingService : IEmbeddingService
    {
        private const int MaxInputChars = 6000;   // keeps us under typical 2k-token limits for code

        private readonly IHttpClientFactory _http;
        private readonly EmbeddingOptions _opt;
        private readonly ILogger<EmbeddingService> _log;

        public EmbeddingService(IHttpClientFactory http, IOptions<EmbeddingOptions> opt, ILogger<EmbeddingService> log)
        {
            _http = http;
            _opt = opt.Value;
            _log = log;
            // NOTE: no throwing here. This service is resolved for every review job; a missing key must only
            // break RAG (handled/logged by the pipeline), never the whole review.
        }

        public async Task<List<float[]>> EmbedAsync(IReadOnlyList<string> texts, bool isQuery, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(_opt.ApiKey))
                throw new InvalidOperationException("Embedding:ApiKey is not configured.");

            var result = new List<float[]>(texts.Count);
            var batchSize = Math.Max(1, _opt.BatchSize);

            for (var i = 0; i < texts.Count; i += batchSize)
            {
                var batch = texts.Skip(i).Take(batchSize)
                    .Select(t => string.IsNullOrWhiteSpace(t) ? " " : HttpRetry.Truncate(t, MaxInputChars))
                    .ToList();

                var vectors = _opt.Provider.Equals("OpenAI", StringComparison.OrdinalIgnoreCase)
                    ? await OpenAiAsync(batch, ct)
                    : await GeminiAsync(batch, isQuery, ct);

                if (vectors.Count != batch.Count)
                    throw new InvalidOperationException($"Embedding API returned {vectors.Count} vectors for {batch.Count} inputs.");

                result.AddRange(vectors);
            }
            return result;
        }

        // ── Gemini ────────────────────────────────────────────────────────────────
        private async Task<List<float[]>> GeminiAsync(List<string> batch, bool isQuery, CancellationToken ct)
        {
            var baseUrl = string.IsNullOrWhiteSpace(_opt.BaseUrl) ? "https://generativelanguage.googleapis.com/v1beta" : _opt.BaseUrl.TrimEnd('/');
            var model = string.IsNullOrWhiteSpace(_opt.Model) ? "gemini-embedding-001" : _opt.Model;
            var url = $"{baseUrl}/models/{model}:batchEmbedContents";

            var payload = new
            {
                requests = batch.Select(t => new Dictionary<string, object>
                {
                    ["model"] = $"models/{model}",
                    ["content"] = new { parts = new[] { new { text = t } } },
                    ["taskType"] = isQuery ? "RETRIEVAL_QUERY" : "RETRIEVAL_DOCUMENT",
                    ["outputDimensionality"] = _opt.Dimensions
                }).ToList()
            };
            var json = JsonSerializer.Serialize(payload);

            var (status, body) = await HttpRetry.SendAsync(_http, () =>
            {
                var req = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
                req.Headers.Add("x-goog-api-key", _opt.ApiKey);
                return req;
            }, _log, ct);

            if ((int)status >= 400)
                throw new InvalidOperationException($"Gemini embedding error {(int)status}: {HttpRetry.Truncate(body, 300)}");

            using var doc = JsonDocument.Parse(body);
            var list = new List<float[]>();
            foreach (var emb in doc.RootElement.GetProperty("embeddings").EnumerateArray())
            {
                var values = emb.GetProperty("values").EnumerateArray().Select(v => v.GetSingle()).ToArray();
                list.Add(Normalize(values));   // Gemini does not normalize when outputDimensionality < 3072
            }
            return list;
        }

        // ── OpenAI / OpenAI-compatible ────────────────────────────────────────────
        private async Task<List<float[]>> OpenAiAsync(List<string> batch, CancellationToken ct)
        {
            var baseUrl = string.IsNullOrWhiteSpace(_opt.BaseUrl) ? "https://api.openai.com/v1" : _opt.BaseUrl.TrimEnd('/');
            var model = string.IsNullOrWhiteSpace(_opt.Model) ? "text-embedding-3-small" : _opt.Model;

            var payload = new Dictionary<string, object> { ["model"] = model, ["input"] = batch };
            if (_opt.Dimensions > 0 && model.StartsWith("text-embedding-3", StringComparison.OrdinalIgnoreCase))
                payload["dimensions"] = _opt.Dimensions;
            var json = JsonSerializer.Serialize(payload);

            var (status, body) = await HttpRetry.SendAsync(_http, () =>
            {
                var req = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/embeddings")
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _opt.ApiKey);
                return req;
            }, _log, ct);

            if ((int)status >= 400)
                throw new InvalidOperationException($"Embedding API error {(int)status}: {HttpRetry.Truncate(body, 300)}");

            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.GetProperty("data").EnumerateArray()
                .OrderBy(d => d.GetProperty("index").GetInt32())
                .Select(d => Normalize(d.GetProperty("embedding").EnumerateArray().Select(v => v.GetSingle()).ToArray()))
                .ToList();
        }

        private static float[] Normalize(float[] v)
        {
            double sum = 0;
            foreach (var x in v) sum += (double)x * x;
            var norm = Math.Sqrt(sum);
            if (norm < 1e-12) return v;
            var r = new float[v.Length];
            for (var i = 0; i < v.Length; i++) r[i] = (float)(v[i] / norm);
            return r;
        }
    }

}
