using Jarvis.Framework.Shared.Events;
using Jarvis.Framework.TestHelpers;

namespace Jarvis.Framework.Tests.ProjectionsTests
{
    public class InsertEvent : DomainEvent
    {
        public string Text { get; set; }
        public InsertEvent()
        {
            this.AssignIdForTest(new MyAggregateId(1));
        }
    }
}