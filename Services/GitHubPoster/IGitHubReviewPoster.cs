using Models.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Services.GitHubPoster
{
    public static class IssueFingerprint
    {
        public static string Compute(string? file, string? category, string description)
        {
            var norm = new string((description ?? "").ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
            if (norm.Length > 80) norm = norm[..80];
            var raw = $"{file}|{category}|{norm}";
            return Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(raw)));   // 40 chars
        }
    }

    public interface IGitHubReviewPoster
    {
        /// <summary>
        /// Posts ONE GitHub review (inline comments on the exact lines + a summary body) and a commit status.
        /// Returns the GitHub review id, or null if nothing was posted.
        /// </summary>
        Task<long?> PostAsync(Models.Entities.PullRequest pr, Review review, IReadOnlyList<ReviewIssue> issues, string token,
            IReadOnlyDictionary<int, string>? ruleTitles = null, CancellationToken ct = default);
    }
}
