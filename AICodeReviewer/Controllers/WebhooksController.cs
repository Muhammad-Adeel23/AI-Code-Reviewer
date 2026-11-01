using Hangfire;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Models.Entities;
using Services.GitHub;
using Services.Rag;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AICodeReviewer.Controllers
{
    [ApiController]
    [Route("api/webhooks")]
    public class WebhooksController : ControllerBase
    {
        private readonly IGitHubConnectService _gitHubConnectService;
        private readonly IConfiguration _config;
        private readonly ILogger<WebhooksController> _logger;

        public WebhooksController(IGitHubConnectService gitHubConnectService, IConfiguration config, ILogger<WebhooksController> logger)
        {
            _gitHubConnectService = gitHubConnectService;
            _config = config;
            _logger = logger;
        }

        // POST /api/webhooks/github   (no [Authorize]: GitHub calls it; the HMAC signature is the authentication)
        [HttpPost("github")]
        public async Task<IActionResult> GitHubWebhook()
        {
            using var reader = new StreamReader(Request.Body, Encoding.UTF8);
            var payload = await reader.ReadToEndAsync();

            var eventType = Request.Headers["X-GitHub-Event"].ToString();
            if (string.IsNullOrEmpty(eventType))
                return BadRequest(new { received = false, message = "Event header missing" });

            // v1 used `?? ""` here: with an empty secret anyone could forge a valid HMAC. Refuse instead.
            var webhookSecret = _config["GitHub:WebhookSecret"];
            if (string.IsNullOrWhiteSpace(webhookSecret))
            {
                _logger.LogError("GitHub:WebhookSecret is not configured - rejecting webhook");
                return StatusCode(500, new { received = false });
            }

            var signature = Request.Headers["X-Hub-Signature-256"].ToString();
            if (!_gitHubConnectService.VerifySignature(payload, signature, webhookSecret))
            {
                _logger.LogWarning("Invalid webhook signature");
                return Unauthorized(new { received = false });
            }

            // v1 used `_ = Task.Run(...)`: that code ran on the request-scoped DbContext, which is DISPOSED as soon as we
            // return 200, so events were randomly lost (exceptions were swallowed inside the repository). Hangfire creates
            // a fresh DI scope per job and gives retries + a dashboard.
            switch (eventType)
            {
                case "pull_request":
                    BackgroundJob.Enqueue<IGitHubConnectService>(s => s.ProcessPrEventAsync(payload));
                    break;

                case "push":   // keeps the vector index fresh (add the "push" event to the webhook, see README)
                    BackgroundJob.Enqueue<IPushEventHandler>(h => h.HandleAsync(payload));
                    break;
            }

            return Ok(new { received = true });
        }
    }
}
