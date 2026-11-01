using Models.ResponseModels.AIReview;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Services.AIReview
{
    public record RulePrompt(int Id, string Title, string Description, string Severity, string? Category);

    /// <summary>One LLM call worth of input: a group of files (annotated diff) + the retrieved repo context for them.</summary>
    public class ReviewBatch
    {
        public string Diff { get; set; } = string.Empty;
        public string? Context { get; set; }
        /// <summary>Rules whose file scope matches at least one file of this batch.</summary>
        public List<RulePrompt> Rules { get; set; } = new();
        public int DiffChars => Diff.Length;
    }

    public class ReviewRequest
    {
        public string PrTitle { get; set; } = string.Empty;
        public string? PrDescription { get; set; }
        public string? RepoInstructions { get; set; }
        public List<ReviewBatch> Batches { get; set; } = new();
        /// <summary>Appended to the summary (e.g. "3 files were not reviewed: size limits").</summary>
        public string? SummaryNote { get; set; }
    }

    public class LlmReply
    {
        public string Text { get; set; } = string.Empty;
        public int PromptTokens { get; set; }
        public int CompletionTokens { get; set; }
    }
    public interface IAiReviewService
    {
        Task<AiReviewResult> ReviewCodeAsync(string diff, string prTitle, string? prDescription);

        /// <summary>Reviews every batch, merges issues/scores, builds one summary.</summary>
        Task<AiReviewResult> ReviewAsync(ReviewRequest request, CancellationToken ct = default);

        /// <summary>Plain chat completion (used for the merged summary and for "ask the codebase").</summary>
        Task<LlmReply> CompleteAsync(string system, string user, int? maxTokens = null, CancellationToken ct = default);
    }
}
