using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Models.DTOs.PullRequestDTO;

namespace Services.PullRequest
{
    public interface IPullRequestService
    {
        Task<PagedResult<PullRequestListDto>> GetAllPullRequestsAsync(
            int userId, PrFilterRequest filter, string orgRole);
        Task<PagedResult<PullRequestListDto>> GetRepoPullRequestsAsync(
            int userId, int repoId, PrFilterRequest filter, string orgRole);
        Task<PullRequestDetailDto> GetPullRequestDetailAsync(int userId, int prId);
        Task<ReviewStatusDto> TriggerReviewAsync(int userId, int prId);
        Task<ReviewStatusDto> GetReviewStatusAsync(int userId, int prId);
    }
}