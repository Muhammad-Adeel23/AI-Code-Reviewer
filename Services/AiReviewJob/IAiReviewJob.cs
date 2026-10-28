using System.Threading.Tasks;

namespace Services.AiReviewJob
{
    public interface IAiReviewJob
    {
        Task ExecuteAsync(int pullRequestId);
    }
}