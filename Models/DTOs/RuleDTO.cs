using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Models.DTOs
{
    public class IndexStatusDto
    {
        public string Status { get; set; } = "NotIndexed";
        public int FilesIndexed { get; set; }
        public int ChunksIndexed { get; set; }
        public DateTime? LastIndexedAt { get; set; }
        public string? LastCommitSha { get; set; }
        public string? LastError { get; set; }
        public bool UseRag { get; set; }
    }

    public class ReviewSettingsRequest
    {
        public string? CustomInstructions { get; set; }
        public bool? UseRag { get; set; }
        public bool? AutoReview { get; set; }
        public string? SeverityThreshold { get; set; }   // ALL | LOW | MEDIUM | HIGH
    }

    public class AskRequest
    {
        public string Question { get; set; } = string.Empty;
    }

    public class AskResponse
    {
        public string Answer { get; set; } = string.Empty;
        public List<string> Sources { get; set; } = new();
    }

    // ── review rules ─────────────────────────────────────────────────────────
    public class RuleDto
    {
        public int Id { get; set; }
        public int? RepositoryId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Severity { get; set; } = "MEDIUM";
        public string? Category { get; set; }
        public string? FilePattern { get; set; }
        public bool IsEnabled { get; set; } = true;
        public DateTime UpdatedAt { get; set; }
    }

    public class UpsertRuleRequest
    {
        /// <summary>null = rule applies to every repository of the organization.</summary>
        public int? RepositoryId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Severity { get; set; } = "MEDIUM";
        public string? Category { get; set; }
        /// <summary>Glob(s), comma separated. Examples: **/Controllers/**/*.cs , src/api/**, *.ts</summary>
        public string? FilePattern { get; set; }
        public bool IsEnabled { get; set; } = true;
    }

    public class RuleTemplateDto
    {
        public string Key { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Severity { get; set; } = "MEDIUM";
        public string? Category { get; set; }
        public string? FilePattern { get; set; }
    }

    // ── evaluation ───────────────────────────────────────────────────────────
    public class EvalRequest { public List<EvalCase> Cases { get; set; } = new(); }
    public class EvalCase { public int PrId { get; set; } public List<ExpectedBug> Expected { get; set; } = new(); }
    public class ExpectedBug
    {
        /// <summary>Path suffix, e.g. "UserService.cs". Optional.</summary>
        public string? File { get; set; }
        /// <summary>Caught if ANY keyword appears in the issue text, e.g. ["null", "NullReference"].</summary>
        public List<string> Keywords { get; set; } = new();
    }
}
