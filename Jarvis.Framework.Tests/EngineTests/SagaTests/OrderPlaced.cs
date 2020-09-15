namespace Jarvis.Framework.Tests.EngineTests.SagaTests
{
    public class OrderPlaced : DomainEvent
    {
        public OrderId OrderId { get; protected set; }

        public OrderPlaced(OrderId id)
        {
            OrderId = id;
        }
    }
}