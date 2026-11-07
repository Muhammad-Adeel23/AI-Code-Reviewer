using Hangfire;
using Microsoft.Extensions.Logging;
using Models.Entities;
using Repository.PullRequest;
using Repository.Teams;
using Services.AiReviewJob;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Models.DTOs.PullRequestDTO;

namespace Services.PullRequest
{
    public class PullRequestService : IPullRequestService
    {
        private readonly IPullRequestRepository _prRepo;
        private readonly ITeamRepository _teamRepo;
        private readonly AppDbContext _db;
        private readonly ILogger<PullRequestService> _logger;

        public PullRequestService(
            IPullRequestRepository prRepo,
            ITeamRepository teamRepo,
            AppDbContext db,
            ILogger<PullRequestService> logger)
        {
            _prRepo = prRepo;
            _teamRepo = teamRepo;
            _db = db;
            _logger = logger;
        }

        // ─── All PRs ──────────────────────────────────────────────────────────────

        public async Task<PagedResult<PullRequestListDto>> GetAllPullRequestsAsync(
            int userId, PrFilterRequest filter, string orgRole)
        {
            var org = await _teamRepo.GetOrganizationByUserAsync(userId)
                ?? throw new KeyNotFoundException("Organization nahi mili");

            return await _prRepo.GetAllAsync(org.Id, userId, filter, orgRole);
        }

        // ─── Repo ki PRs ─────────────────────────────────────────────────────────

        public async Task<PagedResult<PullRequestListDto>> GetRepoPullRequestsAsync(
            int userId, int repoId, PrFilterRequest filter, string orgRole)
        {
            var org = await _teamRepo.GetOrganizationByUserAsync(userId)
                ?? throw new KeyNotFoundException("Organization nahi mili");

            // Repo is org ki hai?
            if (!await _prRepo.IsRepoOwnedByOrgAsync(repoId, org.Id))
                throw new UnauthorizedAccessException("Repository access nahi hai");

            return await _prRepo.GetByRepoAsync(repoId, userId, filter, orgRole);
        }

        // ─── PR Detail ────────────────────────────────────────────────────────────

        public async Task<PullRequestDetailDto> GetPullRequestDetailAsync(
            int userId, int prId)
        {
            var org = await _teamRepo.GetOrganizationByUserAsync(userId)
                ?? throw new KeyNotFoundException("Organization nahi mili");

            if (!await _prRepo.IsPrInOrgAsync(prId, org.Id))
                throw new UnauthorizedAccessException("PR access nahi hai");

            return await _prRepo.GetByIdAsync(prId)
                ?? throw new KeyNotFoundException("PR nahi mili");
        }

        // ─── Manual Review Trigger ────────────────────────────────────────────────

        public async Task<ReviewStatusDto> TriggerReviewAsync(int userId, int prId)
        {
            var org = await _teamRepo.GetOrganizationByUserAsync(userId)
                ?? throw new KeyNotFoundException("Organization nahi mili");

            if (!await _prRepo.IsPrInOrgAsync(prId, org.Id))
                throw new UnauthorizedAccessException("PR access nahi hai");

            var pr = await _prRepo.GetEntityByIdAsync(prId)
                ?? throw new KeyNotFoundException("PR nahi mili");

            // Already reviewing check
            var busy = pr.ReviewStatus is "Analyzing" or "Queued" or "Reviewing";
            var stale = pr.UpdatedAt < DateTime.UtcNow.AddMinutes(-15);
            if (busy && !stale)
                throw new InvalidOperationException("Review pehle se chal rahi hai - wait karein");

            // Status update karo
            await _prRepo.UpdateReviewStatusAsync(prId, "Queued");

                          
            var jobId = BackgroundJob.Enqueue<IAiReviewJob>(j => j.ExecuteAsync(prId, null));

            return new ReviewStatusDto
            {
                Status = "Queued",
                Progress = 0,
                CurrentStep = "Review queue mein add ho gayi",
                JobId = jobId
            };
        }

        // ─── Review Status ────────────────────────────────────────────────────────

        public async Task<ReviewStatusDto> GetReviewStatusAsync(int userId, int prId)
        {
            var org = await _teamRepo.GetOrganizationByUserAsync(userId)
           ?? throw new KeyNotFoundException("PR nahi mili");
            if (!await _prRepo.IsPrInOrgAsync(prId, org.Id))
                throw new KeyNotFoundException("PR nahi mili");

            var pr = await _prRepo.GetEntityByIdAsync(prId)
                ?? throw new KeyNotFoundException("PR nahi mili");

            return pr.ReviewStatus switch
            {
                "Pending" => new ReviewStatusDto { Status = "Pending", Progress = 0, CurrentStep = "Review pending hai" },
                "Queued" => new ReviewStatusDto { Status = "Queued", Progress = 10, CurrentStep = "Queue mein hai..." },
                "Analyzing" => new ReviewStatusDto { Status = "Analyzing", Progress = 50, CurrentStep = "AI review kar raha hai..." },
                "Completed" => new ReviewStatusDto { Status = "Completed", Progress = 100, CurrentStep = "Review complete" },
                "Failed" => new ReviewStatusDto { Status = "Failed", Progress = 0, CurrentStep = "Review fail ho gayi" },
                _ => new ReviewStatusDto { Status = "Unknown", Progress = 0, CurrentStep = "" }
            };
        }
    }
}