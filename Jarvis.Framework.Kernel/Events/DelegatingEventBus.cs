//using System;
//using Jarvis.Framework.Shared.Events;

//namespace Jarvis.Framework.Kernel.Events
//{
//    public sealed class DelegatingEventBus : IEventBus
//    {
//        private readonly Action<DomainEvent> _action;

//        public DelegatingEventBus(Action<DomainEvent> action)
//        {
//            _action = action;
//        }

//        public void Publish(DomainEvent e)
//        {
//            _action(e);
//        }
//    }
//}
