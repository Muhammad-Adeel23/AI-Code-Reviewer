using Microsoft.EntityFrameworkCore;
using Models.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Repository.AIReview
{
    public class ReviewRepository : IReviewRepository
    {
        private readonly AppDbContext _db;

        public ReviewRepository(AppDbContext db)
        {
            _db = db;
        }

        public async Task<Review> CreateAsync(Review review)
        {
            _db.Reviews.Add(review);
            await _db.SaveChangesAsync();
            return review;
        }

        public async Task<Review?> GetLatestByPrAsync(int prId)
        {
            return await _db.Reviews
                .Include(r => r.ReviewIssues)
                .Where(r => r.PullRequestId == prId
                         && r.Status == "Completed")
                .OrderByDescending(r => r.Version)
                .FirstOrDefaultAsync();
        }

        public async Task<List<Review>> GetHistoryByPrAsync(int prId)
        {
            return await _db.Reviews
                .Where(r => r.PullRequestId == prId
                         && r.Status == "Completed")
                .OrderBy(r => r.Version)
                .ToListAsync();
        }

        public async Task<Review?> GetByIdAsync(int reviewId)
        {
            return await _db.Reviews
                .Include(r => r.ReviewIssues)
                .FirstOrDefaultAsync(r => r.Id == reviewId);
        }

        public async Task UpdateAsync(Review review)
        {
            _db.Reviews.Update(review);
            await _db.SaveChangesAsync();
        }

        public async Task<int> GetNextVersionAsync(int prId)
        {
            var maxVersion = await _db.Reviews
                .Where(r => r.PullRequestId == prId)
                .MaxAsync(r => (int?)r.Version) ?? 0;

            return maxVersion + 1;
        }

        public async Task<List<ReviewIssue>> GetIssuesByReviewAsync(int reviewId)
        {
            return await _db.ReviewIssues
                .Where(ri => ri.ReviewId == reviewId)
                .OrderBy(ri => ri.Severity == "HIGH" ? 1
                             : ri.Severity == "MEDIUM" ? 2
                             : ri.Severity == "LOW" ? 3
                             : 4)
                .ToListAsync();
        }

        public async Task UpdateIssueStatusAsync(
            int issueId, string status, int updatedBy)
        {
            var issue = await _db.ReviewIssues.FindAsync(issueId);
            if (issue is null) return;

            issue.Status = status;
            issue.StatusUpdatedAt = DateTime.UtcNow;
            issue.StatusUpdatedBy = updatedBy;

            await _db.SaveChangesAsync();
        }
    }
}
