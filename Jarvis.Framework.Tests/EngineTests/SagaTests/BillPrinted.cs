using Jarvis.Framework.Shared.Events;

namespace Jarvis.Framework.Tests.EngineTests.SagaTests
{
    public class BillPrinted : DomainEvent
    {
        public OrderId OrderId { get; protected set; }

        public BillPrinted(OrderId id)
        {
            OrderId = id;
        }
    }
}