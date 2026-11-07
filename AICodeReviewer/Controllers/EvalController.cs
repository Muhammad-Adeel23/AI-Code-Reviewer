using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Models.ApiResponseModels;
using Models.DTOs;
using Services.Eval;
using System.Security.Claims;

namespace AICodeReviewer.Controllers
{
    [ApiController]
    [Route("api/eval")]
    [Authorize]
    public class EvalController : BaseController
    {
        private readonly IEvalService _eval;
        public EvalController(IEvalService eval) => _eval = eval;

        // POST /api/eval/run   (Owner only) - see README for the request format
        [HttpPost("run")]
        public async Task<IActionResult> Run([FromBody] EvalRequest request, CancellationToken ct)
        {
            if (!string.Equals(User.FindFirstValue("orgRole"), "Owner", StringComparison.OrdinalIgnoreCase))
                return StatusCode(403, ApiResponse<object>.Fail("Sirf Owner eval chala sakta hai"));

            return Ok(ApiResponse<object>.Ok(await _eval.RunAsync((int)_OrganizationId, request, ct)));
        }
    }
}
