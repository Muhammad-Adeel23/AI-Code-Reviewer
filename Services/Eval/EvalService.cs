using Models.DTOs;
using Repository.ReviewExecutionRepository;
using Services.ReviewPipeline;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Services.Eval
{
    public class EvalService : IEvalService
    {
        private readonly IReviewExecutionRepository _repo;
        private readonly IReviewPipeline _pipeline;

        public EvalService(IReviewExecutionRepository repo, IReviewPipeline pipeline)
        {
            _repo = repo; _pipeline = pipeline;
        }

        public async Task<object> RunAsync(int organizationId, EvalRequest request, CancellationToken ct)
        {
            if (request.Cases.Count is 0 or > 30)
                throw new ArgumentException("1 se 30 cases do");

            var rows = new List<object>();
            var agg = new Dictionary<string, Agg> { ["baseline"] = new(), ["rag"] = new() };

            foreach (var c in request.Cases)
            {
                var pr = await _repo.GetPullRequestForReviewAsync(c.PrId, organizationId, ct);
                if (pr is null) { rows.Add(new { c.PrId, error = "PR not found in your organisation" }); continue; }

                var perMode = new Dictionary<string, object>();
                foreach (var (mode, useRag) in new[] { ("baseline", false), ("rag", true) })
                {
                    var sw = Stopwatch.StartNew();
                    var r = await _pipeline.RunAsync(pr, useRag, ct);
                    sw.Stop();

                    var caught = c.Expected.Count(e => r.Result.Issues.Any(i => Matches(i.FileName, i.Description, i.Suggestion, e)));
                    var a = agg[mode];
                    a.Expected += c.Expected.Count; a.Caught += caught; a.Cases++;
                    a.PromptTokens += r.Result.PromptTokens; a.Ms += sw.ElapsedMilliseconds;
                    a.Issues += r.Result.Issues.Count; a.ContextChunks += r.ContextChunks;

                    perMode[mode] = new
                    {
                        caught,
                        expected = c.Expected.Count,
                        issues = r.Result.Issues.Count,
                        promptTokens = r.Result.PromptTokens,
                        ms = sw.ElapsedMilliseconds,
                        ragUsed = r.UsedRag,
                        contextChunks = r.ContextChunks
                    };
                }
                rows.Add(new { c.PrId, results = perMode });
            }

            return new { baseline = Summ(agg["baseline"]), rag = Summ(agg["rag"]), cases = rows };
        }

        private static object Summ(Agg a) => new
        {
            recall = a.Expected == 0 ? 0 : Math.Round(100.0 * a.Caught / a.Expected, 1),
            caught = a.Caught,
            expected = a.Expected,
            avgPromptTokens = a.Cases == 0 ? 0 : a.PromptTokens / a.Cases,
            avgLatencyMs = a.Cases == 0 ? 0 : a.Ms / a.Cases,
            avgIssues = a.Cases == 0 ? 0 : Math.Round((double)a.Issues / a.Cases, 1),
            avgContextChunks = a.Cases == 0 ? 0 : Math.Round((double)a.ContextChunks / a.Cases, 1)
        };

        private static bool Matches(string? file, string desc, string? suggestion, ExpectedBug e)
        {
            if (!string.IsNullOrEmpty(e.File) && (file == null || !file.EndsWith(e.File, StringComparison.OrdinalIgnoreCase)))
                return false;
            var hay = $"{desc} {suggestion}";
            return e.Keywords.Count == 0 || e.Keywords.Any(k => hay.Contains(k, StringComparison.OrdinalIgnoreCase));
        }

        private class Agg { public int Cases, Expected, Caught, PromptTokens, Issues, ContextChunks; public long Ms; }
    }

}
