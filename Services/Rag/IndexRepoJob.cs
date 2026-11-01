using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Services.Rag
{
    public class IndexRepoJob : IIndexRepoJob
    {
        private readonly IRepoIndexer _indexer;
        public IndexRepoJob(IRepoIndexer indexer) => _indexer = indexer;

        public Task ExecuteAsync(int repositoryId, bool force) => _indexer.IndexAsync(repositoryId, force);
    }
}
