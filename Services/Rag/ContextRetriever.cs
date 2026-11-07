using Microsoft.AspNetCore.Builder.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Models.Entities;
using Repository.Rag;
using System.Text;
using System.Text.RegularExpressions;


namespace Services.Rag
{
    public class ContextRetriever : IContextRetriever
    {
        private static readonly Regex CallRx = new(@"\b([A-Za-z_]\w{2,})\s*\(", RegexOptions.Compiled);
        private static readonly Regex IdentRx = new(@"\b[A-Za-z_][A-Za-z0-9_]{3,}\b", RegexOptions.Compiled);

        private static readonly HashSet<string> Stop = new(StringComparer.Ordinal)
    {
        "if","for","foreach","while","switch","catch","using","lock","return","new","nameof","typeof","sizeof",
        "await","async","throw","else","try","function","class","this","base","super","print","println","require",
        "ToString","ToList","ToArray","Add","Remove","Select","Where","Any","All","First","FirstOrDefault","Count",
        "Contains","Equals","GetType","Length","Join","Split","Trim","Replace","Format","Parse","TryParse","Log",
        "LogInformation","LogError","LogWarning","Console","WriteLine","Task","Run","Delay","Dispose","Close","Open",
        "map","filter","reduce","forEach","push","pop","then","catch","setTimeout","console","useState","useEffect"
    };

        private readonly IRepoIndexRepository _index;
        private readonly IEmbeddingService _embeddings;
        private readonly IVectorStore _vectors;
        private readonly RagOptions _opt;
        private readonly ILogger<ContextRetriever> _log;

        public ContextRetriever(IRepoIndexRepository index, IEmbeddingService embeddings, IVectorStore vectors,
            IOptions<RagOptions> opt, ILogger<ContextRetriever> log)
        {
            _index = index; _embeddings = embeddings; _vectors = vectors; _opt = opt.Value; _log = log;
        }

        public Task<bool> IsReadyAsync(int repoId, CancellationToken ct = default) =>
            _index.HasIndexAsync(repoId, ct);

        public async Task<RetrievedContext> BuildForFilesAsync(int repoId, IReadOnlyList<PullRequestFile> files, CancellationToken ct = default)
        {
            if (!_opt.Enabled || files.Count == 0 || !await IsReadyAsync(repoId, ct))
                return RetrievedContext.Empty;

            var changedPaths = files.Select(f => f.FileName).ToHashSet(StringComparer.Ordinal);

            // 1) exact definitions of what the diff calls
            var called = ExtractCalledSymbols(files);
            var symbolHits = new List<ScoredChunk>();
            if (called.Count > 0)
            {
                var raw = await _vectors.FindBySymbolsAsync(repoId, called, _opt.MaxSymbolChunks * 2, ct);
                symbolHits = raw.Where(c => !IsMostlyPatchedFile(c, changedPaths)).Take(_opt.MaxSymbolChunks).ToList();
            }

            // 2) semantic neighbours of each changed file's added code
            var queries = files
                .Where(f => !string.IsNullOrEmpty(f.Patch))
                .Take(_opt.MaxQueryFiles)
                .Select(f => (f.FileName, Text: BuildQuery(f)))
                .Where(q => q.Text.Length > 40)
                .ToList();

            var semantic = new List<ScoredChunk>();
            if (queries.Count > 0)
            {
                var vectors = await _embeddings.EmbedAsync(queries.Select(q => q.Text).ToList(), isQuery: true, ct);
                for (var i = 0; i < queries.Count; i++)
                {
                    var hits = await _vectors.SearchAsync(repoId, vectors[i], _opt.TopKPerFile + 2, _opt.MinScore, ct);
                    // the same file's base version is nearly identical to the patch -> little value
                    semantic.AddRange(hits.Where(h => h.FilePath != queries[i].FileName).Take(_opt.TopKPerFile));
                }
            }

            var ordered = symbolHits.Concat(semantic.OrderByDescending(s => s.Score));
            return Assemble(ordered, called);
        }

        public async Task<RetrievedContext> BuildForQuestionAsync(int repoId, string question, int maxChunks, CancellationToken ct = default)
        {
            if (!_opt.Enabled || !await IsReadyAsync(repoId, ct)) return RetrievedContext.Empty;

            var idents = IdentRx.Matches(question).Select(m => m.Value)
                .Where(v => v.Any(char.IsUpper) && v.Skip(1).Any(char.IsLower) || v.Contains('_'))
                .Distinct().Take(_opt.MaxSymbolLookups).ToList();

            var symbolHits = idents.Count > 0
                ? await _vectors.FindBySymbolsAsync(repoId, idents, maxChunks, ct)
                : new List<ScoredChunk>();

            var vec = (await _embeddings.EmbedAsync(new[] { question }, isQuery: true, ct))[0];
            var semantic = await _vectors.SearchAsync(repoId, vec, maxChunks, Math.Min(_opt.MinScore, 0.4), ct);

            return Assemble(symbolHits.Concat(semantic), idents);
        }

        // ── helpers ──────────────────────────────────────────────────────────────

        private RetrievedContext Assemble(IEnumerable<ScoredChunk> ordered, IReadOnlyCollection<string> called)
        {
            var sb = new StringBuilder();
            var sources = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var perFile = new Dictionary<string, int>(StringComparer.Ordinal);
            int symbolCount = 0, semanticCount = 0;

            foreach (var c in ordered)
            {
                var key = $"{c.FilePath}:{c.StartLine}";
                if (!seen.Add(key)) continue;
                if (perFile.GetValueOrDefault(c.FilePath) >= 3) continue;

                var label = c.Kind == "symbol"
                    ? "defines: " + string.Join(", ", c.Symbols.Intersect(called).Take(3))
                    : $"similar code (score {c.Score:0.00})";
                var block = $"### {c.FilePath} (lines {c.StartLine}-{c.EndLine}) - {label}\n```\n{c.Content}\n```\n\n";

                if (sb.Length + block.Length > _opt.MaxContextChars) continue;   // try smaller ones that may still fit

                sb.Append(block);
                sources.Add($"{c.FilePath}:{c.StartLine}-{c.EndLine}");
                perFile[c.FilePath] = perFile.GetValueOrDefault(c.FilePath) + 1;
                if (c.Kind == "symbol") symbolCount++; else semanticCount++;
            }

            return new RetrievedContext(sb.ToString(), sources.Count, symbolCount, semanticCount, sources);
        }

        private static bool IsMostlyPatchedFile(ScoredChunk c, HashSet<string> changed) =>
            changed.Contains(c.FilePath) && c.Kind != "symbol";

        private static string BuildQuery(PullRequestFile f)
        {
            var added = (f.Patch ?? "").Split('\n')
                .Where(l => l.StartsWith('+') && !l.StartsWith("+++"))
                .Select(l => l[1..]);
            var text = string.Join("\n", added);
            if (text.Length > 1500) text = text[..1500];
            return $"{f.FileName}\n{text}".Trim();
        }

        private List<string> ExtractCalledSymbols(IReadOnlyList<PullRequestFile> files)
        {
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var f in files)
            {
                if (string.IsNullOrEmpty(f.Patch)) continue;
                foreach (var line in f.Patch.Split('\n'))
                {
                    if (!line.StartsWith('+') || line.StartsWith("+++")) continue;
                    foreach (Match m in CallRx.Matches(line))
                    {
                        var name = m.Groups[1].Value;
                        if (Stop.Contains(name)) continue;
                        counts[name] = counts.GetValueOrDefault(name) + 1;
                    }
                }
            }
            return counts.OrderByDescending(kv => kv.Value).Select(kv => kv.Key).Take(_opt.MaxSymbolLookups).ToList();
        }
    }

}
