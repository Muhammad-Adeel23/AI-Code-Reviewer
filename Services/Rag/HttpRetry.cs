using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Services.Rag
{
    internal static class HttpRetry
    {
        public static async Task<(HttpStatusCode Status, string Body)> SendAsync(
            IHttpClientFactory factory,
            Func<HttpRequestMessage> build,
            ILogger log,
            CancellationToken ct,
            int maxAttempts = 4)
        {
            var client = factory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(120);

            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    using var req = build();
                    using var resp = await client.SendAsync(req, ct);
                    var body = await resp.Content.ReadAsStringAsync(ct);

                    var retryable = resp.StatusCode == HttpStatusCode.TooManyRequests || (int)resp.StatusCode >= 500;
                    if (!retryable || attempt >= maxAttempts)
                        return (resp.StatusCode, body);

                    var wait = resp.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(Math.Pow(2, attempt));
                    if (wait > TimeSpan.FromSeconds(30)) wait = TimeSpan.FromSeconds(30);
                    log.LogWarning("HTTP {Status} (attempt {A}/{Max}) - retrying in {Wait}s",
                        (int)resp.StatusCode, attempt, maxAttempts, wait.TotalSeconds);
                    await Task.Delay(wait, ct);
                }
                catch (HttpRequestException ex) when (attempt < maxAttempts)
                {
                    log.LogWarning(ex, "Network error (attempt {A}/{Max})", attempt, maxAttempts);
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), ct);
                }
                catch (TaskCanceledException) when (!ct.IsCancellationRequested && attempt < maxAttempts)
                {
                    log.LogWarning("HTTP timeout (attempt {A}/{Max})", attempt, maxAttempts);
                }
            }
        }

        public static string Truncate(string? s, int max) =>
            string.IsNullOrEmpty(s) ? "" : (s.Length <= max ? s : s[..max] + "...");
    }

}
