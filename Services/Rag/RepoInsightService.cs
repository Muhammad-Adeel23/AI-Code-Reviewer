using Hangfire;
using Models.ApiResponseModels;
using Models.DTOs;
using Repository.Rag;
using Services.AIReview;


namespace Services.Rag
{
    public class RepoInsightService : IRepoInsightService
    {
        private static readonly string[] Thresholds = { "ALL", "LOW", "MEDIUM", "HIGH" };

        private readonly IRepoSettingsRepository _settings;
        private readonly IRepoIndexRepository _index;
        private readonly IContextRetriever _retriever;
        private readonly IAiReviewService _ai;

        public RepoInsightService(IRepoSettingsRepository settings, IRepoIndexRepository index,
            IContextRetriever retriever, IAiReviewService ai)
        {
            _settings = settings; _index = index; _retriever = retriever; _ai = ai;
        }

        private async Task<RepoSettingsView> RequireRepoAsync(int organizationId, int repositoryId) =>
            await _settings.GetAsync(organizationId, repositoryId)
            ?? throw new KeyNotFoundException("Repository nahi mili");

        public async Task<IndexStatusDto> GetIndexStatusAsync(int organizationId, int repositoryId)
        {
            var repo = await RequireRepoAsync(organizationId, repositoryId);
            var s = await _index.GetStateAsync(repositoryId);
            return new IndexStatusDto
            {
                Status = s?.Status ?? "NotIndexed",
                FilesIndexed = s?.FilesIndexed ?? 0,
                ChunksIndexed = s?.ChunksIndexed ?? 0,
                LastIndexedAt = s?.LastIndexedAt,
                LastCommitSha = s?.LastCommitSha,
                LastError = s?.LastError,
                UseRag = repo.UseRag
            };
        }

        public async Task StartIndexingAsync(int organizationId, int repositoryId, bool force)
        {
            await RequireRepoAsync(organizationId, repositoryId);

            var state = await _index.GetStateAsync(repositoryId);
            if (state?.Status == "Indexing" && state.StartedAt > DateTime.UtcNow.AddMinutes(-30))
                throw new InvalidOperationException("Indexing pehle se chal rahi hai");

            await _index.EnsureStateAsync(repositoryId);
            BackgroundJob.Enqueue<IIndexRepoJob>(j => j.ExecuteAsync(repositoryId, force));
        }

        public async Task UpdateSettingsAsync(int organizationId, int repositoryId, ReviewSettingsRequest request)
        {
            await RequireRepoAsync(organizationId, repositoryId);

            if (request.CustomInstructions is { Length: > 2000 })
                throw new ArgumentException("Custom instructions 2000 characters se chhote hon");

            var threshold = request.SeverityThreshold?.Trim().ToUpperInvariant();
            if (threshold != null && !Thresholds.Contains(threshold))
                throw new ArgumentException("SeverityThreshold: ALL | LOW | MEDIUM | HIGH");

            await _settings.UpdateAsync(organizationId, repositoryId, request.CustomInstructions?.Trim(),
                request.UseRag, request.AutoReview, threshold);
        }

        public async Task<AskResponse> AskAsync(int organizationId, int repositoryId, string question, CancellationToken ct)
        {
            await RequireRepoAsync(organizationId, repositoryId);

            if (string.IsNullOrWhiteSpace(question) || question.Length > 1000)
                throw new ArgumentException("Question 1-1000 characters ka hona chahiye");

            var ctx = await _retriever.BuildForQuestionAsync(repositoryId, question, 8, ct);
            if (ctx.IsEmpty)
                return new AskResponse { Answer = "Is repository ka index abhi ready nahi hai (ya kuch relevant nahi mila)." };

            var reply = await _ai.CompleteAsync(
                "You are a codebase assistant. Answer ONLY from the repository excerpts provided. " +
                "Cite file paths and line ranges. If the excerpts do not contain the answer, say so plainly. Be concise.",
                $"Question: {question}\n\nRepository excerpts:\n{ctx.Text}", 1200, ct);

            return new AskResponse { Answer = reply.Text, Sources = ctx.Sources };
        }
    }

}
