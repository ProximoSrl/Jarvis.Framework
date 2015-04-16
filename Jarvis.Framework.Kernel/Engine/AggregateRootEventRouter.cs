using System;
using System.Threading;
using Jarvis.Framework.Kernel.Store;
using Jarvis.Framework.Shared.Events;

namespace Jarvis.Framework.Kernel.Engine
{
    public class AggregateRootEventRouter<TState> : NEventStoreEx.CommonDomainEx.Core.ConventionEventRouterEx where TState : AggregateState, new()
    {
        private AggregateRoot<TState> _source;

        public void AttachAggregateRoot(AggregateRoot<TState> source)
        {
            if (source == null) throw new ArgumentNullException("source");
            _source = source;
        }

        public override void Dispatch(object eventMessage)
        {
            if (eventMessage == null)
                throw new ArgumentNullException("eventMessage");

            var evt = (DomainEvent)eventMessage;

            // execute
            _source.Apply(evt);
        }
    }
}