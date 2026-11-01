using Hangfire;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Services.Rag
{
    public interface IIndexRepoJob
    {
        // Hangfire reads filter attributes from the INTERFACE method when you Enqueue<IIndexRepoJob>.
        // DisableConcurrentExecution locks on type+method (not args) => only one index job runs at a time,
        // which also keeps us inside free-tier embedding rate limits.
        [DisableConcurrentExecution(timeoutInSeconds: 1800)]
        [AutomaticRetry(Attempts = 1, DelaysInSeconds = new[] { 120 })]
        Task ExecuteAsync(int repositoryId, bool force);
    }
}
