using Models.ApiResponseModels;
using Models.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Services.Rag
{
    public interface IRepoInsightService
    {
        Task<IndexStatusDto> GetIndexStatusAsync(int organizationId, int repositoryId);
        Task StartIndexingAsync(int organizationId, int repositoryId, bool force);
        Task UpdateSettingsAsync(int organizationId, int repositoryId, ReviewSettingsRequest request);
        Task<AskResponse> AskAsync(int organizationId, int repositoryId, string question, CancellationToken ct);
    }
}
