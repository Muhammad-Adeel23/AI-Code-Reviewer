using Microsoft.AspNetCore.Builder.Extensions;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Services.Rag
{
    public class CodeChunker : ICodeChunker
    {
        private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs", ".ts", ".tsx", ".js", ".jsx", ".mjs", ".py", ".java", ".kt", ".go", ".rb", ".php",
        ".rs", ".swift", ".c", ".h", ".cpp", ".hpp", ".cc", ".scala", ".vue", ".sql", ".dart", ".razor", ".cshtml"
    };

        private static readonly string[] SkipDirs =
        {
        "/node_modules/", "/bin/", "/obj/", "/dist/", "/build/", "/.git/", "/vendor/", "/packages/",
        "/migrations/", "/wwwroot/lib/", "/__pycache__/", "/.next/", "/coverage/", "/.vs/"
    };

        private static readonly string[] SkipSuffixes =
        {
        ".min.js", ".designer.cs", ".g.cs", ".generated.cs", ".d.ts", ".snap"
    };

        // Lines that look like the start of a declaration -> good place to split.
        private static readonly Regex Boundary = new(
            @"^\s{0,8}(?:///|\[\w|@\w|(?:(?:public|private|protected|internal|static|async|export|default|abstract|override|virtual|sealed|partial|final|unsafe)\s+)+\S|(?:class|interface|struct|enum|record|function|def|func|fn|namespace|module|impl|trait|type)\s+\w)",
            RegexOptions.Compiled);

        // Declared names.
        private static readonly Regex[] SymbolPatterns =
        {
        new(@"\b(?:class|interface|struct|enum|record|trait|impl|type)\s+([A-Za-z_]\w*)", RegexOptions.Compiled),
        new(@"\b(?:def|function|fn)\s+([A-Za-z_]\w*)", RegexOptions.Compiled),
        new(@"\bfunc\s+(?:\([^)]*\)\s*)?([A-Za-z_]\w*)\s*\(", RegexOptions.Compiled),
        new(@"\b(?:const|let|var)\s+([A-Za-z_]\w*)\s*=\s*(?:async\s*)?(?:\([^)]*\)|[A-Za-z_]\w*)\s*=>", RegexOptions.Compiled),
        // C#/Java/Kotlin-like: modifiers + return type + Name(
        new(@"^\s*(?:\[[^\]]*\]\s*)*(?:(?:public|private|protected|internal|static|async|override|virtual|abstract|sealed|final|export|default|unsafe|extern|new|partial)\s+)+(?:[\w<>\[\],.?*&]+\s+)+?([A-Za-z_]\w*)\s*(?:<[^>(]*>)?\s*\(",
            RegexOptions.Compiled | RegexOptions.Multiline),
        // JS/TS class methods: name(args) {
        new(@"^\s{2,8}(?:async\s+)?([A-Za-z_]\w*)\s*\([^)]*\)\s*(?::\s*[\w<>\[\]|.? ]+)?\s*\{", RegexOptions.Compiled | RegexOptions.Multiline),
    };

        private static readonly HashSet<string> NotSymbols = new(StringComparer.Ordinal)
    {
        "if", "for", "foreach", "while", "switch", "catch", "using", "lock", "return", "new", "get", "set",
        "else", "try", "finally", "do", "case", "when", "constructor", "super", "this", "function", "fixed"
    };

        private readonly RagOptions _opt;

        public CodeChunker(IOptions<RagOptions> opt) => _opt = opt.Value;

        public bool IsIndexable(string path, long sizeBytes)
        {
            if (sizeBytes <= 0 || sizeBytes > _opt.MaxFileBytes) return false;

            var lower = "/" + path.Replace('\\', '/').ToLowerInvariant();
            if (!Extensions.Contains(Path.GetExtension(lower))) return false;
            if (SkipSuffixes.Any(lower.EndsWith)) return false;
            return !SkipDirs.Any(lower.Contains);
        }

        public List<CodeChunk> Chunk(string filePath, string content)
        {
            var lines = content.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            var chunks = new List<CodeChunk>();
            var max = Math.Max(400, _opt.ChunkMaxChars);
            var min = Math.Min(Math.Max(100, _opt.ChunkMinChars), max - 100);
            var overlap = Math.Max(0, _opt.ChunkOverlapLines);

            var i = 0;
            while (i < lines.Length)
            {
                var start = i;
                var chars = 0;
                var j = i;
                var softBreak = false;

                while (j < lines.Length)
                {
                    var len = lines[j].Length + 1;
                    if (chars + len > max && j > start) break;          // hard limit reached
                    chars += len;
                    j++;

                    if (chars >= min && j < lines.Length && Boundary.IsMatch(lines[j]))
                    {
                        softBreak = true;                                // next line starts a new declaration
                        break;
                    }
                }

                var text = string.Join("\n", lines[start..j]);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    chunks.Add(new CodeChunk(chunks.Count, start + 1, j, text, ExtractSymbols(text)));
                }

                // overlap only when we had to cut in the middle of something
                i = (!softBreak && j < lines.Length && overlap > 0)
                    ? Math.Max(start + 1, j - overlap)
                    : j;
            }
            return chunks;
        }

        public static List<string> ExtractSymbols(string text)
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            foreach (var rx in SymbolPatterns)
                foreach (Match m in rx.Matches(text))
                {
                    var name = m.Groups[1].Value;
                    if (name.Length >= 3 && !NotSymbols.Contains(name)) set.Add(name);
                    if (set.Count >= 25) return set.ToList();
                }
            return set.ToList();
        }
    }

}
