using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Repository.Rag;
using System.Formats.Tar;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Services.Rag
{
    public class RepoIndexer : IRepoIndexer
    {
        private readonly IRepoIndexRepository _index;
        private readonly IHttpClientFactory _http;
        private readonly ICodeChunker _chunker;
        private readonly IEmbeddingService _embeddings;
        private readonly IVectorStore _vectors;
        private readonly RagOptions _opt;
        private readonly ILogger<RepoIndexer> _log;

        public RepoIndexer(IRepoIndexRepository index, IHttpClientFactory http, ICodeChunker chunker,
            IEmbeddingService embeddings, IVectorStore vectors, IOptions<RagOptions> opt, ILogger<RepoIndexer> log)
        {
            _index = index; _http = http; _chunker = chunker; _embeddings = embeddings;
            _vectors = vectors; _opt = opt.Value; _log = log;
        }

        public async Task IndexAsync(int repositoryId, bool force, CancellationToken ct = default)
        {
            var target = await _index.GetIndexTargetAsync(repositoryId, ct)
                         ?? throw new InvalidOperationException($"Repository {repositoryId} not found");

            var token = target.GitHubToken;
            if (string.IsNullOrEmpty(token))
                throw new InvalidOperationException("Repository owner has no GitHub token (reconnect GitHub).");

            var begin = await _index.BeginIndexingAsync(repositoryId, ct);

            try
            {
                await _vectors.EnsureCollectionAsync(ct);

                var headSha = await GetHeadShaAsync(target, token, ct);
                if (!force && begin.WasReady && begin.LastCommitSha == headSha)
                {
                    _log.LogInformation("Index {Repo}: already at {Sha}, nothing to do", target.FullName, headSha[..7]);
                    await _index.MarkUpToDateAsync(repositoryId, ct);
                    return;
                }

                var existing = await _index.GetFileHashesAsync(repositoryId, ct);

                // 1) download tarball once, keep only new/changed indexable files
                var (changed, seen) = await DownloadAsync(target, token, headSha, existing, force, ct);

                // 2) remove vectors of deleted + changed files
                var deleted = existing.Keys.Where(p => !seen.Contains(p)).ToList();
                var stale = deleted.Concat(changed.Keys.Where(existing.ContainsKey)).ToList();
                if (stale.Count > 0) await _vectors.DeleteByFilesAsync(repositoryId, stale, ct);

                // 3) chunk -> embed -> upsert (batched)
                var pending = new List<(string Path, CodeChunk Chunk)>();
                var chunkCounts = new Dictionary<string, int>(StringComparer.Ordinal);
                foreach (var (path, file) in changed)
                {
                    var chunks = _chunker.Chunk(path, file.Content);
                    chunkCounts[path] = chunks.Count;
                    pending.AddRange(chunks.Select(c => (path, c)));
                }

                const int batchSize = 32;
                for (var i = 0; i < pending.Count; i += batchSize)
                {
                    ct.ThrowIfCancellationRequested();
                    var batch = pending.Skip(i).Take(batchSize).ToList();
                    var vectors = await _embeddings.EmbedAsync(
                        batch.Select(b => BuildEmbedText(b.Path, b.Chunk)).ToList(), isQuery: false, ct);

                    var points = batch.Select((b, k) => new VectorPoint(
                        QdrantVectorStore.PointId(repositoryId, b.Path, b.Chunk.Index),
                        vectors[k],
                        new Dictionary<string, object?>
                        {
                            ["repo_id"] = repositoryId,
                            ["file_path"] = b.Path,
                            ["start_line"] = b.Chunk.StartLine,
                            ["end_line"] = b.Chunk.EndLine,
                            ["symbols"] = b.Chunk.Symbols,
                            ["content"] = b.Chunk.Content
                        })).ToList();

                    await _vectors.UpsertAsync(points, ct);
                    if (_opt.EmbedDelayMs > 0) await Task.Delay(_opt.EmbedDelayMs, ct);
                }

                // 4) persist per-file hashes + state (so the next run only touches what changed)
                var records = changed.Select(kv => new IndexedFileRecord(kv.Key, kv.Value.Hash, chunkCounts[kv.Key])).ToList();
                await _index.CompleteIndexingAsync(repositoryId, headSha, records, deleted, ct);

                _log.LogInformation("Index {Repo} done: {Changed} changed, {Deleted} deleted, {Chunks} chunks embedded",
                    target.FullName, changed.Count, deleted.Count, pending.Count);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Indexing failed for {Repo}", target.FullName);
                try { await _index.FailIndexingAsync(repositoryId, ex.Message); } catch { /* ignore */ }
                throw;
            }
        }

        private static string BuildEmbedText(string path, CodeChunk c) =>
            $"// File: {path}\n// Symbols: {string.Join(", ", c.Symbols)}\n{c.Content}";

        private static HttpRequestMessage GitHubRequest(string url, string token)
        {
            var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            req.Headers.UserAgent.ParseAdd("AICodeReviewer");
            req.Headers.Accept.ParseAdd("application/vnd.github+json");
            req.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
            return req;
        }

        private async Task<string> GetHeadShaAsync(IndexTarget repo, string token, CancellationToken ct)
        {
            var url = $"https://api.github.com/repos/{repo.FullName}/commits/{Uri.EscapeDataString(repo.DefaultBranch)}";
            var (status, body) = await HttpRetry.SendAsync(_http, () => GitHubRequest(url, token), _log, ct);
            if ((int)status >= 400)
                throw new InvalidOperationException($"GitHub commits API {(int)status}: {HttpRetry.Truncate(body, 200)}");

            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.GetProperty("sha").GetString()
                   ?? throw new InvalidOperationException("GitHub returned no commit sha");
        }

        private async Task<(Dictionary<string, (string Content, string Hash)> Changed, HashSet<string> Seen)>
            DownloadAsync(IndexTarget repo, string token, string sha, Dictionary<string, string> existing,
                          bool force, CancellationToken ct)
        {
            var changed = new Dictionary<string, (string, string)>(StringComparer.Ordinal);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            long total = 0;
            var truncated = false;

            var client = _http.CreateClient();
            client.Timeout = Timeout.InfiniteTimeSpan;   // big repos: body streaming is bounded by the CancellationToken

            using var req = GitHubRequest($"https://api.github.com/repos/{repo.FullName}/tarball/{sha}", token);
            using var resp = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!resp.IsSuccessStatusCode)
                throw new InvalidOperationException($"GitHub tarball {(int)resp.StatusCode}");

            await using var net = await resp.Content.ReadAsStreamAsync(ct);
            await using var gz = new GZipStream(net, CompressionMode.Decompress);
            await using var tar = new TarReader(gz);

            TarEntry? entry;
            while ((entry = await tar.GetNextEntryAsync(copyData: false, ct)) is not null)
            {
                if (entry.EntryType is not (TarEntryType.RegularFile or TarEntryType.V7RegularFile)) continue;

                var slash = entry.Name.IndexOf('/');
                if (slash < 0) continue;
                var path = entry.Name[(slash + 1)..];                   // strip "owner-repo-sha/"

                if (path.Length > 500 || entry.DataStream is null) continue;
                if (!_chunker.IsIndexable(path, entry.Length)) continue;
                if (seen.Count >= _opt.MaxFilesPerIndex || total + entry.Length > _opt.MaxTotalBytes)
                {
                    truncated = true;
                    continue;
                }

                using var ms = new MemoryStream((int)entry.Length);
                await entry.DataStream.CopyToAsync(ms, ct);
                var bytes = ms.ToArray();
                if (Array.IndexOf(bytes, (byte)0) >= 0) continue;       // binary file

                seen.Add(path);
                var hash = Convert.ToHexString(SHA256.HashData(bytes));
                if (!force && existing.TryGetValue(path, out var old) && old == hash) continue;

                changed[path] = (Encoding.UTF8.GetString(bytes), hash);
                total += bytes.Length;
            }

            if (truncated)
                _log.LogWarning("Index {Repo}: limits reached (MaxFilesPerIndex/MaxTotalBytes) - some files were skipped", repo.FullName);

            return (changed, seen);
        }
    }
}
