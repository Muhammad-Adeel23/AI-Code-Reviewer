using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Models.ResponseModels.AIReview
{
    public class AiReviewResult
    {
        public int CodeQualityScore { get; set; }
        public int SecurityScore { get; set; }
        public int MaintainabilityScore { get; set; }
        public int OverallScore { get; set; }
        public bool MergeRecommendation { get; set; }
        public string AiSummary { get; set; } = string.Empty;
        public List<AiIssue> Issues { get; set; } = new();
        public int PromptTokens { get; set; }
        public int CompletionTokens { get; set; }
    }

    public class AiIssue
    {
        public string Severity { get; set; } = string.Empty;
        // HIGH | MEDIUM | LOW | SUGGESTION
        public string? FileName { get; set; }
        public int? LineNumber { get; set; }
        public string Description { get; set; } = string.Empty;
        public string? Suggestion { get; set; }
        public string? Category { get; set; }
        // Security | Performance | Style | Logic
        public int? RuleId { get; set; }
    }
}
