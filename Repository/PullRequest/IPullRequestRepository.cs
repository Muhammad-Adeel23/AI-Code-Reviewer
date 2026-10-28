using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Models.Entities;
using static Models.DTOs.PullRequestDTO;

namespace Repository.PullRequest
{
    public interface IPullRequestRepository
    {
        Task<PagedResult<PullRequestListDto>> GetAllAsync(
            int organizationId, int userId, PrFilterRequest filter, string orgRole);
        Task<PagedResult<PullRequestListDto>> GetByRepoAsync(
            int repoId, int userId, PrFilterRequest filter, string orgRole);

        // Detail
        Task<PullRequestDetailDto?> GetByIdAsync(int prId);
        Task<Models.Entities.PullRequest?> GetEntityByIdAsync(int prId);

        // Save
        Task UpdateReviewStatusAsync(int prId, string reviewStatus);

        // Repo ownership check
        Task<bool> IsRepoOwnedByOrgAsync(int repoId, int organizationId);
        Task<bool> IsPrInOrgAsync(int prId, int organizationId);
    }
}
