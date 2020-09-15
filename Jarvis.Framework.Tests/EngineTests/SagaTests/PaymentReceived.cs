namespace Jarvis.Framework.Tests.EngineTests.SagaTests
{
    public class PaymentReceived : DomainEvent
    {
        public string BillId { get; protected set; }
        public OrderId OrderId { get; protected set; }

        public PaymentReceived(OrderId orderId, Guid billId)
        {
            OrderId = orderId;
            BillId = billId.ToString();
        }
    }
}