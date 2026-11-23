using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Models.ApiResponseModels;
using Models.DTOs;
using Models.Entities;
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
        private readonly AppDbContext _dbContext;
        public EvalController(IEvalService eval, AppDbContext dbContext)
        {
            _eval = eval;
            _dbContext = dbContext;
        }

        // POST /api/eval/run   (Owner only) - see README for the request format
        [HttpPost("run")]
        public async Task<IActionResult> Run([FromBody] EvalRequest request, CancellationToken ct)
        {
            var tests = _dbContext.Users.FindAsync();
            var test = "Data Source=db5435.public.databaseasp.net;Initial Catalog=db5435; User ID=db5435;Password=Test@12344;Connect Timeout=30;Encrypt=False;TrustServerCertificate=False;ApplicationIntent=ReadWrite;MultiSubnetFailover=False;";


            if (!string.Equals(User.FindFirstValue("orgRole"), "Owner", StringComparison.OrdinalIgnoreCase))
                return StatusCode(403, ApiResponse<object>.Fail("Sirf Owner eval chala sakta hai"));

            return Ok(ApiResponse<object>.Ok(await _eval.RunAsync((int)_OrganizationId, request, ct)));
        }
    }
}
