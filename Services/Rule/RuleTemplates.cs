using Models.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Services.Rule
{
    public static class RuleTemplates
    {
        public static readonly IReadOnlyList<RuleTemplateDto> All = new List<RuleTemplateDto>
    {
        new() { Key = "no-db-in-controllers", Severity = "MEDIUM", Category = "Architecture", FilePattern = "**/Controllers/**",
            Title = "Controllers must not access the database",
            Description = "Controllers must not use DbContext, SqlConnection, raw SQL or run queries. Controllers call services only; data access belongs in the repository layer." },
        new() { Key = "no-db-in-services", Severity = "MEDIUM", Category = "Architecture", FilePattern = "**/Services/**",
            Title = "Services must not use DbContext directly",
            Description = "Service classes must not inject or use DbContext / SqlConnection. All database access goes through repository interfaces." },
        new() { Key = "no-hardcoded-secrets", Severity = "HIGH", Category = "Security",
            Title = "No hardcoded keys or secrets",
            Description = "API keys, passwords, tokens, connection strings, client secrets and encryption keys must never be hardcoded in source code. Read them from configuration, environment variables or a secret store." },
        new() { Key = "no-sql-concat", Severity = "HIGH", Category = "Security",
            Title = "No SQL built by string concatenation",
            Description = "Never build SQL by concatenating or interpolating user input. Use parameterized queries or the ORM." },
        new() { Key = "authorize-endpoints", Severity = "HIGH", Category = "Security", FilePattern = "**/Controllers/**",
            Title = "Endpoints must be protected",
            Description = "Every new controller action must have [Authorize] (or inherit it) unless it is intentionally public; public endpoints need a comment explaining why." },
        new() { Key = "no-sensitive-logging", Severity = "HIGH", Category = "Security",
            Title = "Do not log secrets or personal data",
            Description = "Log statements must not contain passwords, tokens, API keys, full request bodies with credentials, or personal data." },
        new() { Key = "no-blocking-async", Severity = "MEDIUM", Category = "Performance", FilePattern = "*.cs",
            Title = "Do not block on async code",
            Description = "Do not call .Result, .Wait() or .GetAwaiter().GetResult() on tasks. Use await all the way up." },
        new() { Key = "httpclient-factory", Severity = "MEDIUM", Category = "Performance", FilePattern = "*.cs",
            Title = "Use IHttpClientFactory",
            Description = "Do not create HttpClient with 'new HttpClient()' per call. Inject IHttpClientFactory or a typed client." },
        new() { Key = "no-empty-catch", Severity = "MEDIUM", Category = "Logic",
            Title = "No swallowed exceptions",
            Description = "Catch blocks must not be empty or silently ignore exceptions. Log, handle, or rethrow." },
        new() { Key = "no-dead-code", Severity = "LOW", Category = "Style",
            Title = "No commented-out code or stray TODOs",
            Description = "Do not leave commented-out code, debug prints, or TODO/FIXME without a ticket reference in production code." },
    };
    }

}
