namespace Jarvis.Framework.Tests.EngineTests.SagaTests
{
    public class DeliverPizzaSagaListener : AbstractProcessManagerListener<DeliverPizzaSaga, DeliverPizzaSagaState>
    {
        public DeliverPizzaSagaListener()
        {
            Map<OrderPlaced>(m => m.OrderId);
            Map<PizzaDelivered>(m => m.OrderId);
            Map<BillPrinted>(m => m.OrderId);
            Map<PaymentReceived>(m => m.OrderId);
        }

        public override string Prefix
        {
            get { return "DeliverPizzaSaga_"; }
        }
    }

    public class DeliverPizzaSaga : AbstractProcessManager<DeliverPizzaSagaState>
    {
    }

    public class DeliverPizzaSagaState : AbstractProcessManagerState
    {
        private readonly ILogger _logger = new ConsoleLogger();

        public void On(PaymentReceived paymentReceived)
        {
            _logger.Debug("Payment received");
        }

        public void On(BillPrinted billPrinted)
        {
            _logger.Debug("Bill printed");
        }

        public void On(PizzaDelivered pizzaDelivered)
        {
            _logger.Debug("Pizza delivered");
        }

        public void On(OrderPlaced orderPlaced)
        {
            _logger.Debug("Order Placed");
        }
    }
}