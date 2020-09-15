namespace Jarvis.Framework.Tests.ProjectionsTests
{
    public class DeleteEvent : DomainEvent
    {
        public DeleteEvent()
        {
            this.AssignIdForTest(new MyAggregateId(1));
        }
    }
}