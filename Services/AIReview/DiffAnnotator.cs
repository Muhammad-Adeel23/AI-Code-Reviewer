using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Services.AIReview
{
    public static class DiffAnnotator
    {
        private static readonly Regex Hunk = new(@"^@@ -\d+(?:,\d+)? \+(\d+)(?:,\d+)? @@", RegexOptions.Compiled);

        /// <summary>
        /// "0045 +    var x = Foo();"   (added line, new-file line 45)
        /// "0046      return x;"        (context line)
        /// "     -    old code"         (removed line, no number)
        /// </summary>
        public static string Annotate(string patch, int maxLines = int.MaxValue)
        {
            var sb = new StringBuilder();
            var newLine = 0;
            var emitted = 0;
            var omitted = 0;

            foreach (var raw in patch.Replace("\r\n", "\n").Split('\n'))
            {
                var h = Hunk.Match(raw);
                if (h.Success)
                {
                    newLine = int.Parse(h.Groups[1].Value);
                    if (emitted < maxLines) sb.AppendLine(raw);
                    continue;
                }
                if (raw.StartsWith('\\')) continue;                  // "\ No newline at end of file"
                if (newLine == 0 && raw.Length == 0) continue;

                string line;
                if (raw.StartsWith('+')) { line = $"{newLine:D4} {raw}"; newLine++; }
                else if (raw.StartsWith('-')) { line = $"     {raw}"; }
                else { line = $"{newLine:D4} {raw}"; newLine++; }

                if (emitted >= maxLines) { omitted++; continue; }
                sb.AppendLine(line);
                emitted++;
            }

            if (omitted > 0) sb.AppendLine($"... [{omitted} more diff lines omitted]");
            return sb.ToString();
        }

        /// <summary>Line numbers (new file) that exist inside a hunk = valid targets for inline comments.</summary>
        public static HashSet<int> CommentableLines(string? patch)
        {
            var set = new HashSet<int>();
            if (string.IsNullOrEmpty(patch)) return set;

            var newLine = 0;
            foreach (var raw in patch.Replace("\r\n", "\n").Split('\n'))
            {
                var h = Hunk.Match(raw);
                if (h.Success) { newLine = int.Parse(h.Groups[1].Value); continue; }
                if (newLine == 0 || raw.StartsWith('\\')) continue;

                if (raw.StartsWith('-')) continue;                   // removed: not on the RIGHT side
                set.Add(newLine);
                newLine++;
            }
            return set;
        }
    }
}
