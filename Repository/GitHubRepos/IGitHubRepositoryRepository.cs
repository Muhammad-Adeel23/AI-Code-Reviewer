using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Models.DTOs.GitHubDTO;

namespace Repository.GitHubRepos
{
    public interface IGitHubRepositoryRepository
    {
        Task<bool> IsAlreadyImportedAsync(long githubRepoId);
        Task<int> SaveRepositoryAsync(Models.Entities.Repository repo);
        Task<List<RepositoryDto>> GetUserRepositoriesAsync(int userId);
        Task<Models.Entities.Repository?> GetByIdAsync(int repoId, int userId);
        Task<string?> GetOwnerGitHubTokenAsync(int repoId);
        Task UpdateLastSyncedAsync(int repoId);
        Task SavePullRequestAsync(Models.Entities.PullRequest pr);
        Task<bool> PrExistsAsync(long githubPrId);
        Task UpdatePrStatusAsync(long githubPrId, string status);
    }
}
