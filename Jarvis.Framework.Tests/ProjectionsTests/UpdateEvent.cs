using Fasterflect;
using Jarvis.Framework.Shared.Events;
using Jarvis.Framework.TestHelpers;
using System;

namespace Jarvis.Framework.Tests.ProjectionsTests
{
    public class UpdateEvent : DomainEvent
    {
        public string Text { get; set; }

        public UpdateEvent(Int64 checkpointToken, Int64 aggregateVersion, Int32 eventPosition)
        {
            this.AssignIdForTest(new MyAggregateId(1));
            this.SetPropertyValue("CheckpointToken", checkpointToken);
            this.SetPropertyValue("EventPosition", eventPosition);
            this.SetPropertyValue("Version", aggregateVersion);
        }
    }
}