using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Models.Entities;
using Services.GitHub;
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

        public WebhooksController(
            IGitHubConnectService gitHubConnectService,
            IConfiguration config,
            ILogger<WebhooksController> logger)
        {
            _gitHubConnectService = gitHubConnectService;
            _config = config;
            _logger = logger;
        }

        // POST /api/webhooks/github
        // GitHub yahan automatically POST karta hai jab PR create/update/close ho
        // No [Authorize] — GitHub public se call karta hai
        [HttpPost("github")]
        public async Task<IActionResult> GitHubWebhook()
        {
            // 1. Raw body parhо
            Request.EnableBuffering();
            using var reader = new StreamReader(Request.Body, Encoding.UTF8);
            var payload = await reader.ReadToEndAsync();

            // 2. Event type lo
            var eventType = Request.Headers["X-GitHub-Event"].ToString();
            if (string.IsNullOrEmpty(eventType))
                return BadRequest(new { received = false, message = "Event header missing" });

            // 3. Signature verify karo
            var signature = Request.Headers["X-Hub-Signature-256"].ToString();
            var webhookSecret = _config["GitHub:WebhookSecret"] ?? "";

            if (!_gitHubConnectService.VerifySignature(payload, signature, webhookSecret))
            {
                _logger.LogWarning("Invalid webhook signature");
                return Unauthorized(new { received = false });
            }

            // 4. Sirf pull_request events handle karo
            if (eventType != "pull_request")
                return Ok(new { received = true });

            // 5. Background mein process karo — GitHub ko foran 200 do
            _ = Task.Run(() => _gitHubConnectService.ProcessPrEventAsync(payload));

            return Ok(new { received = true });
        }

       
    }
}
