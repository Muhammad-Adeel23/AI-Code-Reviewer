using Hangfire;
using Microsoft.AspNetCore.Builder.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Repository.Rag;

namespace Services.Rag
{
    public class PushEventHandler : IPushEventHandler
    {
        private readonly IRepoIndexRepository _index;
        private readonly RagOptions _opt;
        private readonly ILogger<PushEventHandler> _log;

        public PushEventHandler(IRepoIndexRepository index, IOptions<RagOptions> opt, ILogger<PushEventHandler> log)
        {
            _index = index; _opt = opt.Value; _log = log;
        }

        public async Task HandleAsync(string payload)
        {
            if (!_opt.Enabled) return;

            using var doc = System.Text.Json.JsonDocument.Parse(payload);
            var root = doc.RootElement;
            var githubRepoId = root.GetProperty("repository").GetProperty("id").GetInt64();
            var reference = root.TryGetProperty("ref", out var r) ? r.GetString() : null;

            var repo = await _index.FindForPushAsync(githubRepoId);
            if (repo is null || !repo.UseRag) return;
            if (reference != $"refs/heads/{repo.DefaultBranch}") return;

            var id = repo.RepositoryId;
            BackgroundJob.Enqueue<IIndexRepoJob>(j => j.ExecuteAsync(id, false));
            _log.LogInformation("Push to {Branch} -> incremental index enqueued for repo {Id}", repo.DefaultBranch, id);
        }
    }

}
