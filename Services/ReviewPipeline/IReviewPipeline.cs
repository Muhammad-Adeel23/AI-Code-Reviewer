using Models.ResponseModels.AIReview;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace Services.ReviewPipeline
{
    public record PipelineResult(
    AiReviewResult Result, bool UsedRag, int ContextChunks, string? ContextSummary,
    List<string> OmittedFiles, IReadOnlyDictionary<int, string> RuleTitles);

    public interface IReviewPipeline
    {
        /// <param name="pr">Must have Repository and PullRequestFiles loaded.</param>
        Task<PipelineResult> RunAsync(Models.Entities.PullRequest pr, bool useRag, CancellationToken ct = default);
    }
}
