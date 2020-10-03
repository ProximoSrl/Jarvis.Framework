using Jarvis.Framework.Shared.Events;
using Jarvis.Framework.TestHelpers;

namespace Jarvis.Framework.Tests.ProjectionsTests
{
    public class UpdateEvent : DomainEvent
    {
        public string Text { get; set; }

        public UpdateEvent()
        {
            this.AssignIdForTest(new MyAggregateId(1));
        }
    }
}