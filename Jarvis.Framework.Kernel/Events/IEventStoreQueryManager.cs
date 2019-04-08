using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Jarvis.Framework.Kernel.Events
{
    /// <summary>
    /// Sometimes we need to do some specific query to the evenstore
    /// that should pass directly with mongo because the standard
    /// client of NeventStore has not the required capabilities.
    /// </summary>
    public interface IEventStoreQueryManager
    {
        /// <summary>
        /// Retrieve all commits for a specific list of aggregates created
        /// after a given checkpoint token. Its primarly use is for offline
        /// system, to retrieve conflicting messages.
        /// </summary>
        /// <param name="checkpointTokenFrom"></param>
        /// <param name="streamIds"></param>
        /// <returns></returns>
        Task<List<CommitShortInfo>> GetCommitsAfterCheckpointTokenAsync(
            Int64 checkpointTokenFrom,
            IEnumerable<String> streamIds);
    }
}
