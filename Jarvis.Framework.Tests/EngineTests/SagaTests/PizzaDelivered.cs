using Jarvis.Framework.Shared.Events;

namespace Jarvis.Framework.Tests.EngineTests.SagaTests
{
	public class PizzaDelivered : DomainEvent
	{
        public OrderId OrderId { get; protected set; }

        public PizzaDelivered(OrderId orderId)
		{
			OrderId = orderId;
		}
	}
}