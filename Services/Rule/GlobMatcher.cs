using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Services.Rule
{
    public static class GlobMatcher
    {
        private static readonly ConcurrentDictionary<string, Regex> Cache = new();

        public static bool IsMatch(string? patterns, string path)
        {
            if (string.IsNullOrWhiteSpace(patterns)) return true;

            var normalized = path.Replace('\\', '/').TrimStart('/');
            foreach (var raw in patterns.Split(new[] { ',', '\n', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (Cache.GetOrAdd(raw, ToRegex).IsMatch(normalized)) return true;
            }
            return false;
        }

        public static bool IsMatchAny(string? patterns, IEnumerable<string> paths) =>
            string.IsNullOrWhiteSpace(patterns) || paths.Any(p => IsMatch(patterns, p));

        private static Regex ToRegex(string glob)
        {
            var g = glob.Replace('\\', '/').TrimStart('/');
            if (!g.Contains('/')) g = "**/" + g;          // "*.cs" -> any folder

            var sb = new StringBuilder("^");
            for (var i = 0; i < g.Length; i++)
            {
                var c = g[i];
                if (c == '*')
                {
                    if (i + 1 < g.Length && g[i + 1] == '*')
                    {
                        i++;
                        if (i + 1 < g.Length && g[i + 1] == '/') { i++; sb.Append("(?:.*/)?"); }   // "**/"
                        else sb.Append(".*");                                                        // "**"
                    }
                    else sb.Append("[^/]*");
                }
                else if (c == '?') sb.Append("[^/]");
                else sb.Append(Regex.Escape(c.ToString()));
            }
            sb.Append('$');
            return new Regex(sb.ToString(), RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);
        }
    }

}
