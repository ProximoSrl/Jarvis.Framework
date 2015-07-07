using Castle.Core.Logging;
using Jarvis.Framework.Kernel.Engine;
using System;

namespace Jarvis.Framework.Tests.EngineTests.SagaTests
{
    public class DeliverPizzaSagaListener2 : AbstractProcessManagerListener<DeliverPizzaSaga2>
    {
        public DeliverPizzaSagaListener2()
        {
            Map<OrderPlaced>(m =>  m.OrderId);
            Map<PizzaDelivered>(m => m.OrderId);
            Map<BillPrinted>(m => m.OrderId);
            Map<PaymentReceived>(m => m.OrderId);
        }

        protected override string Prefix
        {
            get { return "DeliverPizzaSaga2_"; }
        }
    }

    public class DeliverPizzaSaga2 : AbstractProcessManager
        , IObserveMessage<OrderPlaced>
        , IObserveMessage<PizzaDelivered>
        , IObserveMessage<BillPrinted>
        , IObserveMessage<PaymentReceived>
    {
		private readonly ILogger _logger = new ConsoleLogger();

        public String PizzaActualStatus { get; set; }

        private void Log(String message)
        {
            _logger.Debug(message);
            PizzaActualStatus = message;
        }

		public void On(PaymentReceived paymentReceived)
		{
            Log("Payment received");
		}

        public void On(BillPrinted billPrinted)
		{
            Log("Bill printed");	

		}

        public void On(PizzaDelivered pizzaDelivered)
		{
			Log("Pizza delivered");	
		}

		public void On(OrderPlaced orderPlaced)
		{
            Log("Order Placed");	
		}
    }
}   