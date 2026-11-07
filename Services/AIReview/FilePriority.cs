using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Services.AIReview
{
    public static class FilePriority
    {
        private static readonly string[] Risky =
        {
        "auth", "login", "password", "token", "jwt", "secret", "crypt", "permission", "role", "payment",
        "billing", "webhook", "security", "session", "oauth", "credential", "connection", "sql", "query"
    };
        private static readonly string[] Entry =
        {
        "program.cs", "startup.cs", "appsettings", ".env", "dockerfile", "docker-compose", ".github/workflows",
        "web.config", "nginx", ".tf", "pipeline", "jenkinsfile"
    };
        private static readonly string[] Layers =
        {
        "/controllers/", "/services/", "/service/", "/repository/", "/repositories/", "/handlers/",
        "/middleware/", "/jobs/", "/api/", "/routes/", "/hooks/", "/server/"
    };
        private static readonly string[] CodeExt =
        {
        ".cs", ".ts", ".tsx", ".js", ".jsx", ".py", ".java", ".kt", ".go", ".rb", ".php", ".rs", ".swift", ".sql", ".cpp", ".c"
    };
        private static readonly string[] DataLike =
        {
        "/dto/", "/dtos/", "/models/", "/entities/", "/viewmodels/", "/requestmodels/", "/responsemodels/", ".dto.", "/types/"
    };

        public static int Score(string path, string status, int additions, int deletions, bool matchesRuleScope)
        {
            var p = "/" + path.Replace('\\', '/').ToLowerInvariant();
            var score = 50;

            if (Risky.Any(p.Contains)) score += 40;
            if (Entry.Any(p.Contains)) score += 30;
            if (Layers.Any(p.Contains)) score += 25;
            if (CodeExt.Any(p.EndsWith)) score += 10;

            if (p.Contains("/test") || p.Contains(".test.") || p.Contains(".spec.") || p.Contains("_test.")) score -= 20;
            if (p.EndsWith(".md") || p.EndsWith(".txt") || p.EndsWith(".rst")) score -= 30;
            if (DataLike.Any(p.Contains)) score -= 25;
            if (p.EndsWith(".css") || p.EndsWith(".scss") || p.EndsWith(".svg") || p.EndsWith(".json") && !p.Contains("appsettings")) score -= 25;
            if (p.Contains("/migrations/")) score -= 40;

            if (status == "removed") score -= 30;
            else if (status == "modified") score += 5;          // changes to existing code carry regression risk

            score += Math.Min(20, (additions + deletions) / 20); // bigger change = more room for bugs
            if (matchesRuleScope) score += 25;                   // an organization rule targets this file
            return score;
        }
    }
}
