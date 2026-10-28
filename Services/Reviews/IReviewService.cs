using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Models.DTOs.AIReviewDTO;

namespace Services.Reviews
{
    public interface IReviewService
    {
        Task<ReviewDetailDto> GetLatestReviewAsync(int userId, int prId);
        Task<List<ReviewHistoryDto>> GetReviewHistoryAsync(int userId, int prId);
        Task UpdateIssueStatusAsync(int userId, int issueId, string status);
    }
}
