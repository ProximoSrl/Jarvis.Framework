using System;
using System.Collections.Generic;

namespace Jarvis.Framework.Shared.Commands.Tracking
{
    /// <summary>
    /// This interface is used to query MessageTraking system
    /// to understand status of commands inside the system
    /// </summary>
    public interface IMessagesTrackerQueryManager
    {
        /// <summary>
        /// This is the most basic of the query, simply return trackig
        /// mondel of a list of command.
        /// </summary>
        /// <param name="idList"></param>
        /// <returns></returns>
        List<TrackedMessageModel> GetByIdList(IEnumerable<String> idList);

        /// <summary>
        /// Get all tracking info for an aggregate id, it allows for filtering
        /// only for commands that are not executed.
        /// </summary>
        /// <param name="query">Object that contains query definition</param>
        /// <param name="limit">Maximum number of commands to retrieve, 
        /// max value can be 1000</param>
        /// <returns></returns>
        List<TrackedMessageModel> Query(
            MessageTrackerQuery query,
            Int32 limit);

        /// <summary>
        /// Used to retrieve a list of commands of the system for a specific
        /// user and with pagination.
        /// </summary>
        /// <param name="userId">User Id that sent the message, it is a needed
        /// parameter.</param>
        /// <param name="pageIndex"></param>
        /// <param name="pageSize"></param>
        /// <returns></returns>
        TrackedMessageModelPaginated GetCommands(
            string userId,
            int pageIndex,
            int pageSize);
    }
}
