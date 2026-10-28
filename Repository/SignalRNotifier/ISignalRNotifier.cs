using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Repository.SignalRNotifier
{
    public interface ISignalRNotifier
    {
        Task NotifyReviewStartedAsync(int userId, int prId, int prNumber, string repoName);
        Task NotifyReviewCompletedAsync(int userId, int prId, int prNumber,
            string repoName, int overallScore, int highIssues, bool mergeRecommendation);
        Task NotifyReviewFailedAsync(int userId, int prId, int prNumber, string errorMessage);
    }
}
