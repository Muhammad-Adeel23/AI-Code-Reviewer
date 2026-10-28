using AICodeReviewer.ReviewerHub;
using Microsoft.AspNetCore.SignalR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Repository.SignalRNotifier
{
    public class SignalRNotifier : ISignalRNotifier
    {
        private readonly IHubContext<ReviewHub> _hubContext;

        public SignalRNotifier(IHubContext<ReviewHub> hubContext)
        {
            _hubContext = hubContext;
        }

        public async Task NotifyReviewStartedAsync(
            int userId, int prId, int prNumber, string repoName)
        {
            await _hubContext.Clients
                .Group($"user-{userId}")
                .SendAsync("ReviewStarted", new
                {
                    prId,
                    prNumber,
                    repoName,
                    message = $"PR #{prNumber} ki review shuru ho gayi"
                });
        }

        public async Task NotifyReviewCompletedAsync(
            int userId, int prId, int prNumber, string repoName,
            int overallScore, int highIssues, bool mergeRecommendation)
        {
            await _hubContext.Clients
                .Group($"user-{userId}")
                .SendAsync("ReviewCompleted", new
                {
                    prId,
                    prNumber,
                    repoName,
                    overallScore,
                    highIssues,
                    mergeRecommendation,
                    message = $"PR #{prNumber} review complete — Score: {overallScore}/100"
                });
        }

        public async Task NotifyReviewFailedAsync(
            int userId, int prId, int prNumber, string errorMessage)
        {
            await _hubContext.Clients
                .Group($"user-{userId}")
                .SendAsync("ReviewFailed", new
                {
                    prId,
                    prNumber,
                    errorMessage,
                    message = $"PR #{prNumber} review fail ho gayi"
                });
        }
    }
}
