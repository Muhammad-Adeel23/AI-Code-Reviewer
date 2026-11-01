using Anthropic.SDK;
using Anthropic.SDK.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Models.ResponseModels.AIReview;
using Services.Rag;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Services.AIReview
{
    public class AiReviewService : IAiReviewService
    {
        private readonly IHttpClientFactory _http;
        private readonly ILogger<AiReviewService> _logger;
        private readonly string _apiKey;
        private readonly string _baseUrl;
        private readonly string _model;
        private readonly int _maxTokens;
        private readonly string? _reasoningEffort;

        public AiReviewService(IConfiguration config, IHttpClientFactory http, ILogger<AiReviewService> logger)
        {
            _http = http;
            _logger = logger;

            _apiKey = config["Groq:ApiKey"] ?? throw new InvalidOperationException("Groq API key is not configured.");
            _baseUrl = (config["Groq:BaseUrl"] ?? "https://api.groq.com/openai/v1").TrimEnd('/');
            _model = config["Groq:Model"] ?? "openai/gpt-oss-120b";
            _maxTokens = config.GetValue("Groq:MaxTokens", 3000);
            _reasoningEffort = config["Groq:ReasoningEffort"];   // optional: "low" | "medium" | "high" (reasoning models only)
        }

        // ── public API ───────────────────────────────────────────────────────────

        public Task<AiReviewResult> ReviewCodeAsync(string diff, string prTitle, string? prDescription) =>
            ReviewAsync(new ReviewRequest
            {
                PrTitle = prTitle,
                PrDescription = prDescription,
                Batches = { new ReviewBatch { Diff = diff } }
            });

        public async Task<AiReviewResult> ReviewAsync(ReviewRequest request, CancellationToken ct = default)
        {
            if (request.Batches.Count == 0)
                throw new InvalidOperationException("Nothing to review (no batches).");

            var partials = new List<(AiReviewResult Result, int Weight)>();
            foreach (var batch in request.Batches)
            {
                var prompt = BuildPrompt(request, batch);
                var result = await ReviewOneAsync(prompt, ct);
                partials.Add((result, Math.Max(1, batch.DiffChars)));
            }

            var merged = Merge(partials);

            merged.AiSummary = partials.Count == 1
                ? partials[0].Result.AiSummary
                : await SummariseAsync(request, partials.Select(p => p.Result.AiSummary).ToList(), ct);

            if (!string.IsNullOrWhiteSpace(request.SummaryNote))
                merged.AiSummary = $"{merged.AiSummary}\n\n{request.SummaryNote}".Trim();

            return merged;
        }

        public async Task<LlmReply> CompleteAsync(string system, string user, int? maxTokens = null, CancellationToken ct = default)
        {
            var body = new Dictionary<string, object>
            {
                ["model"] = _model,
                ["max_tokens"] = maxTokens ?? _maxTokens,
                ["temperature"] = 0.1,
                ["messages"] = new[]
                {
                    new { role = "system", content = system },
                    new { role = "user", content = user }
                }
            };
            if (!string.IsNullOrWhiteSpace(_reasoningEffort)) body["reasoning_effort"] = _reasoningEffort;
            var json = JsonSerializer.Serialize(body);

            var sw = System.Diagnostics.Stopwatch.StartNew();
            var (status, resp) = await HttpRetry.SendAsync(_http, () =>
            {
                var req = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/chat/completions")
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
                return req;
            }, _logger, ct);

            if ((int)status >= 400)
            {
                _logger.LogError("LLM API error {Status}: {Body}", (int)status, HttpRetry.Truncate(resp, 500));
                throw new InvalidOperationException($"LLM API error {(int)status}: {HttpRetry.Truncate(resp, 200)}");
            }

            using var doc = JsonDocument.Parse(resp);
            var choice = doc.RootElement.GetProperty("choices")[0];
            var text = choice.GetProperty("message").TryGetProperty("content", out var c) ? c.GetString() ?? "" : "";
            var finish = choice.TryGetProperty("finish_reason", out var fr) ? fr.GetString() : null;

            if (string.IsNullOrWhiteSpace(text))
                throw new InvalidOperationException(
                    $"LLM returned empty content (finish_reason={finish}). If this is a reasoning model, raise Groq:MaxTokens or set Groq:ReasoningEffort=low.");

            var usage = doc.RootElement.TryGetProperty("usage", out var u) ? u : default;
            var reply = new LlmReply
            {
                Text = text,
                PromptTokens = usage.ValueKind == JsonValueKind.Object && usage.TryGetProperty("prompt_tokens", out var pt) ? pt.GetInt32() : 0,
                CompletionTokens = usage.ValueKind == JsonValueKind.Object && usage.TryGetProperty("completion_tokens", out var ct2) ? ct2.GetInt32() : 0
            };

            _logger.LogInformation("LLM call {Ms}ms | tokens {In}/{Out} | finish={Finish}",
                sw.ElapsedMilliseconds, reply.PromptTokens, reply.CompletionTokens, finish);
            return reply;
        }

        // ── single batch ─────────────────────────────────────────────────────────

        private const string SystemPrompt =
            "You are an expert senior software engineer performing automated GitHub Pull Request code reviews. " +
            "Return ONLY one valid JSON object. No markdown fences, no text outside the JSON.";

        private async Task<AiReviewResult> ReviewOneAsync(string prompt, CancellationToken ct)
        {
            Exception? last = null;
            for (var attempt = 1; attempt <= 2; attempt++)
            {
                var reply = await CompleteAsync(SystemPrompt, prompt, null, ct);
                try
                {
                    var result = ParseResponse(reply.Text);
                    result.PromptTokens = reply.PromptTokens;
                    result.CompletionTokens = reply.CompletionTokens;
                    return result;
                }
                catch (Exception ex)
                {
                    last = ex;
                    _logger.LogWarning(ex, "Could not parse LLM JSON (attempt {A}/2). Raw: {Raw}", attempt, HttpRetry.Truncate(reply.Text, 300));
                }
            }
            throw new InvalidOperationException("LLM did not return valid review JSON after 2 attempts: " + last?.Message);
        }

        private static string BuildPrompt(ReviewRequest req, ReviewBatch batch)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"PR Title: {req.PrTitle}");
            sb.AppendLine($"PR Description: {(string.IsNullOrWhiteSpace(req.PrDescription) ? "No description provided" : req.PrDescription)}");
            sb.AppendLine();

            if (!string.IsNullOrWhiteSpace(req.RepoInstructions))
            {
                sb.AppendLine("TEAM RULES FOR THIS REPOSITORY (follow these when judging the code):");
                sb.AppendLine(req.RepoInstructions);
                sb.AppendLine();
            }

            if (batch.Rules.Count > 0)
            {
                sb.AppendLine("ORGANIZATION RULES - the team requires every change to follow these. Check the changed code against each rule that applies to the file.");
                foreach (var rule in batch.Rules)
                    sb.AppendLine($"- [rule {rule.Id}] ({rule.Severity}) {rule.Title}: {rule.Description}");
                sb.AppendLine("If the changed code violates a rule, report an issue for it: use the rule's severity (or higher), put the rule id in \"ruleId\", and quote what violates it. Do not invent violations.");
                sb.AppendLine();
            }

            if (!string.IsNullOrWhiteSpace(batch.Context))
            {
                sb.AppendLine("REPOSITORY CONTEXT - existing code from the default branch, retrieved because the diff calls or resembles it.");
                sb.AppendLine("Use it ONLY to verify how the changed code interacts with the rest of the codebase (null handling, signatures,");
                sb.AppendLine("return values, conventions). Do NOT report issues in the context code itself.");
                sb.AppendLine(batch.Context);
            }

            sb.AppendLine("CODE DIFF. Each line starts with its line number in the NEW file (4 digits). '+' = added, ' ' = context, '-' = removed (no number).");
            sb.AppendLine(batch.Diff);
            sb.AppendLine();
            sb.AppendLine("""
Provide your review as ONE JSON object with exactly this structure:
{
  "codeQualityScore": <0-100>,
  "securityScore": <0-100>,
  "maintainabilityScore": <0-100>,
  "overallScore": <0-100>,
  "mergeRecommendation": <true|false>,
  "aiSummary": "<2-3 sentence summary of the changes in this diff>",
  "issues": [
    {
      "severity": "<HIGH|MEDIUM|LOW|SUGGESTION>",
      "fileName": "<exact file path from the diff headers>",
      "lineNumber": <the 4-digit number printed in front of an ADDED ('+') line, as an integer; null only if no single line applies>,
      "description": "<what is wrong and why it matters; mention the repository context you relied on, if any>",
      "suggestion": "<concrete fix, short code snippet allowed>",
      "category": "<Security|Performance|Style|Logic|Architecture>",
      "ruleId": <integer id of the ORGANIZATION RULE this violates, or null if it is a general finding>
    }
  ]
}

Rules:
- HIGH: security vulnerabilities, bugs that will crash or corrupt data, broken contracts with code shown in the context.
- MEDIUM: performance problems, missing error handling, risky patterns. LOW: minor improvements. SUGGESTION: optional best practices.
- Only report problems in the ADDED/changed lines. Never invent files, lines or APIs you were not shown.
- If the context shows that a called function can return null / throw / has a different signature, and the diff ignores that, report it (usually HIGH or MEDIUM).
- mergeRecommendation is true only if there is NO high severity issue.
- Keep each description and suggestion under 600 characters. Maximum 12 issues, most important first.
- Return ONLY the JSON object.
""");
            return sb.ToString();
        }

        // ── multi batch ──────────────────────────────────────────────────────────

        private static AiReviewResult Merge(List<(AiReviewResult Result, int Weight)> parts)
        {
            if (parts.Count == 1) return parts[0].Result;

            var totalW = parts.Sum(p => p.Weight);
            int W(Func<AiReviewResult, int> f) => (int)Math.Round(parts.Sum(p => f(p.Result) * (double)p.Weight) / totalW);

            var seen = new HashSet<string>();
            var issues = new List<AiIssue>();
            foreach (var i in parts.SelectMany(p => p.Result.Issues))
            {
                var key = $"{i.FileName}|{i.LineNumber}|{(i.Description.Length > 60 ? i.Description[..60] : i.Description)}";
                if (seen.Add(key)) issues.Add(i);
            }

            return new AiReviewResult
            {
                CodeQualityScore = W(r => r.CodeQualityScore),
                SecurityScore = W(r => r.SecurityScore),
                MaintainabilityScore = W(r => r.MaintainabilityScore),
                OverallScore = W(r => r.OverallScore),
                MergeRecommendation = parts.All(p => p.Result.MergeRecommendation),
                Issues = issues,
                PromptTokens = parts.Sum(p => p.Result.PromptTokens),
                CompletionTokens = parts.Sum(p => p.Result.CompletionTokens)
            };
        }

        private async Task<string> SummariseAsync(ReviewRequest req, List<string> partSummaries, CancellationToken ct)
        {
            try
            {
                var reply = await CompleteAsync(
                    "You write concise pull-request review summaries. Plain text only.",
                    $"PR title: {req.PrTitle}\nThese are summaries of different parts of one PR:\n- " +
                    string.Join("\n- ", partSummaries) +
                    "\n\nWrite ONE summary of 2-3 sentences for the whole PR.", 400, ct);
                return reply.Text.Trim();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Summary synthesis failed, falling back to concatenation");
                return string.Join(" ", partSummaries.Take(3));
            }
        }

        // ── parsing ──────────────────────────────────────────────────────────────

        private static AiReviewResult ParseResponse(string content)
        {
            var start = content.IndexOf('{');
            var end = content.LastIndexOf('}');
            if (start < 0 || end <= start) throw new FormatException("No JSON object in response");

            using var doc = JsonDocument.Parse(content[start..(end + 1)]);
            var root = doc.RootElement;

            var issues = new List<AiIssue>();
            if (root.TryGetProperty("issues", out var arr) && arr.ValueKind == JsonValueKind.Array)
            {
                foreach (var i in arr.EnumerateArray())
                {
                    var sev = Str(i, "severity")?.Trim().ToUpperInvariant() ?? "LOW";
                    if (sev is not ("HIGH" or "MEDIUM" or "LOW" or "SUGGESTION")) sev = "LOW";

                    issues.Add(new AiIssue
                    {
                        Severity = sev,
                        FileName = Str(i, "fileName"),
                        LineNumber = Int(i, "lineNumber"),
                        Description = Str(i, "description") ?? "",
                        Suggestion = Str(i, "suggestion"),
                        Category = Str(i, "category"),
                        RuleId = Int(i, "ruleId")
                    });
                }
            }

            return new AiReviewResult
            {
                CodeQualityScore = Clamp(Int(root, "codeQualityScore") ?? 70),
                SecurityScore = Clamp(Int(root, "securityScore") ?? 70),
                MaintainabilityScore = Clamp(Int(root, "maintainabilityScore") ?? 70),
                OverallScore = Clamp(Int(root, "overallScore") ?? 70),
                MergeRecommendation = Bool(root, "mergeRecommendation"),
                AiSummary = Str(root, "aiSummary") ?? "",
                Issues = issues
            };
        }

        private static int Clamp(int v) => Math.Max(0, Math.Min(100, v));

        private static string? Str(JsonElement e, string name) =>
            e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

        private static int? Int(JsonElement e, string name)
        {
            if (!e.TryGetProperty(name, out var v)) return null;
            if (v.ValueKind == JsonValueKind.Number) return v.TryGetInt32(out var i) ? i : (int)Math.Round(v.GetDouble());
            if (v.ValueKind == JsonValueKind.String && int.TryParse(v.GetString(), out var p)) return p;   // "0045"
            return null;
        }

        private static bool Bool(JsonElement e, string name)
        {
            if (!e.TryGetProperty(name, out var v)) return false;
            return v.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.String => bool.TryParse(v.GetString(), out var b) && b,
                _ => false
            };
        }
    }
}


