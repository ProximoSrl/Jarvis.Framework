﻿using Castle.Core.Logging;
using Jarvis.Framework.Kernel.Engine;
using Jarvis.Framework.Shared.Commands;
using System;

namespace Jarvis.Framework.Tests.EngineTests.SagaTests
{
    public class DeliverPizzaSagaListener2 : AbstractProcessManagerListener<DeliverPizzaSaga2, DeliverPizzaSaga2State>
    {
        public DeliverPizzaSagaListener2()
        {
            Map<OrderPlaced>(m => m.OrderId);
            Map<PizzaDelivered>(m => m.OrderId);
            Map<BillPrinted>(m => m.OrderId);
            Map<PaymentReceived>(m => m.OrderId);
        }

        public override string Prefix
        {
            get { return "DeliverPizzaSaga2_"; }
        }
    }

    public class DeliverPizzaSaga2 : AbstractProcessManager<DeliverPizzaSaga2State>
    {
        public DeliverPizzaSaga2State AccessState => this.State;
    }

    public class DeliverPizzaSaga2State : AbstractProcessManagerState
    {
        public DeliverPizzaSaga2State()
        {
            Logger = NullLogger.Instance;
        }

        public String PizzaActualStatus { get; set; }

        public int EventCount { get; private set; }

        public int ReplayEventCount { get; private set; }

        private void Log(String message)
        {
            Logger.Debug(message);
            PizzaActualStatus = message;
            EventCount++;
            if (IsInReplay)
            {
                ReplayEventCount++;
            }
        }

        public void On(PaymentReceived _)
        {
            Log("Payment received");
        }

        public void On(BillPrinted _)
        {
            Log("Bill printed");
        }

        public void On(PizzaDelivered _)
        {
            Log("Pizza delivered");
        }

        public ProcessPayment On(OrderPlaced orderPlaced)
        {
            Log("Order Placed");
            return new ProcessPayment()
            {
                PaymentId = $"Payment_{orderPlaced.OrderId}"
            };
        }

        public class ProcessPayment : Command
        {
            public String PaymentId { get; set; }
        }
    }
}