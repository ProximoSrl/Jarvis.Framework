using Fasterflect;
using Jarvis.Framework.Shared.Events;
using Jarvis.Framework.TestHelpers;
using System;

namespace Jarvis.Framework.Tests.ProjectionsTests
{
    public class DeleteEvent : DomainEvent
    {
        public DeleteEvent(Int64 checkpointToken, Int64 aggregateVersion, Int32 eventPosition)
        {
            this.AssignIdForTest(new MyAggregateId(1));
            this.SetPropertyValue("CheckpointToken", checkpointToken);
            this.SetPropertyValue("EventPosition", eventPosition);
            this.SetPropertyValue("Version", aggregateVersion);
        }
    }
}