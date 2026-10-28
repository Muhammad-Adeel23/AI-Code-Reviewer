using Models.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Repository.AIReview
{
    public interface IReviewRepository
    {
        Task<Review> CreateAsync(Review review);
        Task<Review?> GetLatestByPrAsync(int prId);
        Task<List<Review>> GetHistoryByPrAsync(int prId);
        Task<Review?> GetByIdAsync(int reviewId);
        Task UpdateAsync(Review review);
        Task<int> GetNextVersionAsync(int prId);
        Task<List<ReviewIssue>> GetIssuesByReviewAsync(int reviewId);
        Task UpdateIssueStatusAsync(int issueId, string status, int updatedBy);
    }
}
