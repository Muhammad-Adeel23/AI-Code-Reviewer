using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Repository.Dashboard
{
    public class DashboardRepository
    {
        public DashboardRepository()
        {
                
        }

        // DashboardService.cs
        //public async Task<DashboardStatsDto> GetStatsAsync(int userId, string orgRole)
        //{
        //    var org = await _teamRepo.GetOrganizationByUserAsync(userId)
        //        ?? throw new KeyNotFoundException("Organization nahi mili");

        //    var user = await _db.Users.FindAsync(userId);

        //    if (orgRole == "Developer")
        //    {
        //        // Developer — sirf apne PRs ki stats
        //        var myPrs = await _db.PullRequests
        //            .Include(pr => pr.Repository)
        //            .Where(pr => pr.Repository.OrganizationId == org.Id
        //                      && pr.AuthorGitHubLogin.ToLower()
        //                         == (user!.GitHubLogin ?? "").ToLower())
        //            .ToListAsync();

        //        return new DashboardStatsDto
        //        {
        //            ConnectedRepos = 0, // Developer ko repos nahi dikhte
        //            TotalPrsReviewed = myPrs.Count(pr => pr.ReviewStatus == "Completed"),
        //            TotalHighIssues = myPrs.Sum(pr => pr.HighIssueCount),
        //            AvgAiScore = myPrs
        //                .Where(pr => pr.LatestAiScore.HasValue)
        //                .Select(pr => (double?)pr.LatestAiScore)
        //                .Average() ?? 0,
        //            RecentPullRequests = myPrs
        //                .OrderByDescending(pr => pr.GitHubUpdatedAt)
        //                .Take(5)
        //                .Select(pr => MapToPrListDto(pr))
        //                .ToList()
        //        };
        //    }

        //    // Owner / Admin / Reviewer — sab stats
        //    var allPrs = await _db.PullRequests
        //        .Include(pr => pr.Repository)
        //        .Where(pr => pr.Repository.OrganizationId == org.Id)
        //        .ToListAsync();

        //    var repos = await _db.Repositories
        //        .Where(r => r.OrganizationId == org.Id && r.IsActive)
        //        .ToListAsync();

        //    return new DashboardStatsDto
        //    {
        //        ConnectedRepos = repos.Count,
        //        TotalPrsReviewed = allPrs.Count(pr => pr.ReviewStatus == "Completed"),
        //        TotalHighIssues = allPrs.Sum(pr => pr.HighIssueCount),
        //        AvgAiScore = allPrs
        //            .Where(pr => pr.LatestAiScore.HasValue)
        //            .Select(pr => (double?)pr.LatestAiScore)
        //            .Average() ?? 0,
        //        RecentPullRequests = allPrs
        //            .OrderByDescending(pr => pr.GitHubUpdatedAt)
        //            .Take(5)
        //            .Select(pr => MapToPrListDto(pr))
        //            .ToList()
        //    };
        //}
    }
}
