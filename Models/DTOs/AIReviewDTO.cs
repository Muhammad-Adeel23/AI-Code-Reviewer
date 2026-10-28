using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Models.DTOs
{
    public class AIReviewDTO
    {
        public class ReviewDetailDto
        {
            public int Id { get; set; }
            public int Version { get; set; }
            public int? CodeQualityScore { get; set; }
            public int? SecurityScore { get; set; }
            public int? MaintainabilityScore { get; set; }
            public int? OverallScore { get; set; }
            public bool? MergeRecommendation { get; set; }
            public string? AiSummary { get; set; }
            public int HighIssueCount { get; set; }
            public int MediumIssueCount { get; set; }
            public int LowIssueCount { get; set; }
            public DateTime? ReviewedAt { get; set; }
            public List<ReviewIssueDto> Issues { get; set; } = new();
        }

        public class ReviewIssueDto
        {
            public int Id { get; set; }
            public string Severity { get; set; } = string.Empty;
            public string? FileName { get; set; }
            public int? LineNumber { get; set; }
            public string Description { get; set; } = string.Empty;
            public string? Suggestion { get; set; }
            public string? Category { get; set; }
            public string Status { get; set; } = string.Empty;
        }

        public class ReviewHistoryDto
        {
            public int Version { get; set; }
            public int? OverallScore { get; set; }
            public int? CodeQualityScore { get; set; }
            public int? SecurityScore { get; set; }
            public int? MaintainabilityScore { get; set; }
            public int HighIssueCount { get; set; }
            public int MediumIssueCount { get; set; }
            public int LowIssueCount { get; set; }
            public bool? MergeRecommendation { get; set; }
            public DateTime? ReviewedAt { get; set; }
            public int? ScoreImprovement { get; set; }
        }
    }
}
