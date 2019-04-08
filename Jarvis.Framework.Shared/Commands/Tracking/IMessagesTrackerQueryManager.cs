using System;
using System.Collections.Generic;

namespace Jarvis.Framework.Shared.Commands.Tracking
{
    public interface IMessagesTrackerQueryManager
    {
        List<TrackedMessageModel> GetByIdList(List<String> idList);
    }
}
