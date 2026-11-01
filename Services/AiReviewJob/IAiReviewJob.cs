using Hangfire;
using Hangfire.Server;
using System.Threading.Tasks;

namespace Services.AiReviewJob
{
    public interface IAiReviewJob
    {
        // NOTE: Hangfire reads these attributes from the INTERFACE method when jobs are enqueued with
        // BackgroundJob.Enqueue<IAiReviewJob>(...). In v1 the attribute sat on the class method, where it is ignored.
        // PerformContext is injected by Hangfire - always pass null when enqueuing: j => j.ExecuteAsync(id, null)
        [AutomaticRetry(Attempts = 2, DelaysInSeconds = new[] { 60, 180 })]
        Task ExecuteAsync(int pullRequestId, PerformContext? context);
    }
}