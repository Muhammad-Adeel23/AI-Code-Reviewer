using Models.ResponseModels.AIReview;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Services.AIReview
{
    public interface IAiReviewService
    {
        Task<AiReviewResult> ReviewCodeAsync(string diff, string prTitle, string? prDescription);
    }
}
