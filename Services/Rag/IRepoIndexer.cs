using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Services.Rag
{
    public interface IRepoIndexer
    {
        /// <summary>Incremental by default: only files whose SHA-256 changed are re-embedded.</summary>
        Task IndexAsync(int repositoryId, bool force, CancellationToken ct = default);
    }
}
