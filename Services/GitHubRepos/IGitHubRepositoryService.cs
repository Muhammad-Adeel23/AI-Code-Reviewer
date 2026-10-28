using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Models.DTOs.GitHubDTO;

namespace Services.GitHubRepos
{
    public interface IGitHubRepositoryService
    {
        Task<List<GitHubRepoDto>> GetGitHubReposAsync(int userId);
        Task<ImportRepositoryResponse> ImportRepositoryAsync(int userId, ImportRepositoryRequest request, int TenantId);
        Task<List<RepositoryDto>> GetImportedReposAsync(int userId);
    }
}
