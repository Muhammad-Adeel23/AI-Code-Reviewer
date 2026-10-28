using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Models.ApiResponseModels;
using Services.GitHubRepos;
using System.Security.Claims;
using static Models.DTOs.GitHubDTO;

namespace AICodeReviewer.Controllers
{
    [ApiController]
    [Route("api/repositories")]
    [Authorize]
    public class RepositoriesController : BaseController
    {
        private readonly IGitHubRepositoryService _repoService;

        public RepositoriesController(IGitHubRepositoryService repoService)
        {
            _repoService = repoService;
        }

        // GET /api/repositories
        // Imported repos list — dashboard ke liye
        [HttpGet]
        public async Task<IActionResult> GetImportedRepos()
        {
            var userId = _userId;
            var repos = await _repoService.GetImportedReposAsync((int)userId);
            return Ok(ApiResponse<object>.Ok(repos));
        }

        // GET /api/github/repositories
        // GitHub se repos fetch karo — import dialog ke liye
        [HttpGet("/api/github/repositories")]
        public async Task<IActionResult> GetGitHubRepos([FromQuery] string? search = null)
        {
            try
            {
                var userId = _userId;
                var repos = await _repoService.GetGitHubReposAsync((int)userId);
                return Ok(ApiResponse<object>.Ok(repos));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ApiResponse<object>.Fail(ex.Message));
            }
        }

        // POST /api/repositories/import
        [HttpPost("import")]
        public async Task<IActionResult> ImportRepository(
            [FromBody] ImportRepositoryRequest request)
        {
            try
            {

                var userId = _userId;
                var result = await _repoService.ImportRepositoryAsync((int)userId, request,(int)_OrganizationId);
                return StatusCode(201, ApiResponse<object>.Ok(result, result.Message));
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(ApiResponse<object>.Fail(ex.Message));
            }
        }
    }
}
