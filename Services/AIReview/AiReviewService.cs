using Anthropic.SDK;
using Anthropic.SDK.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Models.ResponseModels.AIReview;
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
        private readonly string _baseUrl;
        private readonly IConfiguration _config;
        private readonly ILogger<AiReviewService> _logger;
        private readonly string _apiKey;
        private readonly string _model;
        private readonly int _maxTokens;

        public AiReviewService(IConfiguration config, ILogger<AiReviewService> logger)
        {
            _logger = logger;

            _apiKey = config["Groq:ApiKey"]
                ?? throw new InvalidOperationException(
                    "Groq API key is not configured.");

            _model = config["Groq:Model"]
                ?? "llama-3.3-70b-versatile";

            _maxTokens = config.GetValue<int>(
                "Groq:MaxTokens",
                4000);
        }

        public async Task<AiReviewResult> ReviewCodeAsync(
    string diff,
    string prTitle,
    string? prDescription)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            var maxDiffChars = 14000;

            if (diff.Length > maxDiffChars)
            {
                diff = diff[..maxDiffChars] +
                       "\n\n[Remaining diff omitted due to token limits.]";
            }

            var prompt = BuildPrompt(diff, prTitle, prDescription);
            var requestBody = JsonSerializer.Serialize(new
            {
                model = _model,
                max_tokens = _maxTokens,
                temperature = 0.1,
                messages = new[]
                {
            new
            {
                role = "system",
                content = """
                    You are an expert senior software engineer performing
                    automated GitHub Pull Request code reviews.

                    Analyze the provided code changes carefully.

                    Return ONLY valid JSON matching the requested schema.
                    Do not use markdown code fences.
                    Do not add explanations outside the JSON.
                    """
            },
            new
            {
                role = "user",
                content = prompt
            }
        }
            });

            using var client = new HttpClient();

            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _apiKey);

            var response = await client.PostAsync(
                "https://api.groq.com/openai/v1/chat/completions",
                new StringContent(
                    requestBody,
                    Encoding.UTF8,
                    "application/json"));

            var json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "Groq API error: {Status} — {Body}",
                    response.StatusCode,
                    json);

                throw new InvalidOperationException(
                    $"Groq API error: {response.StatusCode} — {json}");
            }

            stopwatch.Stop();

            using var doc = JsonDocument.Parse(json);

            var content = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? "";

            var usage = doc.RootElement.GetProperty("usage");

            var promptTokens = usage
                .GetProperty("prompt_tokens")
                .GetInt32();

            var completionTokens = usage
                .GetProperty("completion_tokens")
                .GetInt32();

            _logger.LogInformation(
                "Groq review complete — {Ms}ms | Tokens: {In}/{Out}",
                stopwatch.ElapsedMilliseconds,
                promptTokens,
                completionTokens);

            return ParseResponse(
                content,
                promptTokens,
                completionTokens);
        }

        // ─── Prompt Builder ───────────────────────────────────────────────────────

        private static string BuildPrompt(
            string diff, string prTitle, string? prDescription)
        {
            return $$"""
You are an expert code reviewer. Analyze this Pull Request and provide a detailed review.

PR Title: {{prTitle}}
PR Description: {{prDescription ?? "No description provided"}}

Code Diff:
{{diff}}

Provide your review as a JSON object with this EXACT structure (no markdown, pure JSON):
{
  "codeQualityScore": <0-100>,
  "securityScore": <0-100>,
  "maintainabilityScore": <0-100>,
  "overallScore": <0-100>,
  "mergeRecommendation": <true/false>,
  "aiSummary": "<2-3 sentence summary of the PR>",
  "issues": [
    {
      "severity": "<HIGH|MEDIUM|LOW|SUGGESTION>",
      "fileName": "<file path or null>",
      "lineNumber": <line number or null>,
      "description": "<clear description of the issue>",
      "suggestion": "<how to fix it>",
      "category": "<Security|Performance|Style|Logic>"
    }
  ]
}

Rules:
- HIGH: Security vulnerabilities, bugs that will cause crashes, data loss risks
- MEDIUM: Performance issues, error handling gaps, code smells
- LOW: Minor improvements, style issues
- SUGGESTION: Best practices, optional improvements
- mergeRecommendation: true only if NO HIGH severity issues
- Be specific with file names and line numbers when possible
- Return ONLY the JSON object, no other text
""";
        }

        private AiReviewResult ParseResponse(
        string content, int promptTokens, int completionTokens)
        {
            try
            {
                // JSON extract karo — agar koi extra text ho
                var jsonStart = content.IndexOf('{');
                var jsonEnd = content.LastIndexOf('}');

                if (jsonStart == -1 || jsonEnd == -1)
                    throw new Exception("Valid JSON nahi mila response mein");

                var json = content[jsonStart..(jsonEnd + 1)];
                var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                var issues = new List<AiIssue>();

                if (root.TryGetProperty("issues", out var issuesArray))
                {
                    foreach (var issue in issuesArray.EnumerateArray())
                    {
                        issues.Add(new AiIssue
                        {
                            Severity = issue.TryGetProperty("severity", out var sev)
                                          ? sev.GetString() ?? "LOW" : "LOW",
                            FileName = issue.TryGetProperty("fileName", out var fn)
                                          ? fn.GetString() : null,
                            LineNumber = issue.TryGetProperty("lineNumber", out var ln)
                                          && ln.ValueKind == JsonValueKind.Number
                                          ? ln.GetInt32() : null,
                            Description = issue.TryGetProperty("description", out var desc)
                                          ? desc.GetString() ?? "" : "",
                            Suggestion = issue.TryGetProperty("suggestion", out var sug)
                                          ? sug.GetString() : null,
                            Category = issue.TryGetProperty("category", out var cat)
                                          ? cat.GetString() : null,
                        });
                    }
                }

                return new AiReviewResult
                {
                    CodeQualityScore = GetInt(root, "codeQualityScore", 70),
                    SecurityScore = GetInt(root, "securityScore", 70),
                    MaintainabilityScore = GetInt(root, "maintainabilityScore", 70),
                    OverallScore = GetInt(root, "overallScore", 70),
                    MergeRecommendation = root.TryGetProperty("mergeRecommendation", out var mr)
                                           && mr.GetBoolean(),
                    AiSummary = root.TryGetProperty("aiSummary", out var summary)
                                           ? summary.GetString() ?? "" : "",
                    Issues = issues,
                    PromptTokens = promptTokens,
                    CompletionTokens = completionTokens
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AI response parse mein masla");

                // Fallback result
                return new AiReviewResult
                {
                    CodeQualityScore = 50,
                    SecurityScore = 50,
                    MaintainabilityScore = 50,
                    OverallScore = 50,
                    MergeRecommendation = false,
                    AiSummary = "Review parse karne mein masla aaya",
                    Issues = new List<AiIssue>(),
                    PromptTokens = promptTokens,
                    CompletionTokens = completionTokens
                };
            }
        }

        private static int GetInt(JsonElement root, string key, int defaultVal)
        {
            return root.TryGetProperty(key, out var val)
                && val.ValueKind == JsonValueKind.Number
                ? val.GetInt32()
                : defaultVal;
        }
    }
}
