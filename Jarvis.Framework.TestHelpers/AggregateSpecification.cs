using Fasterflect;
using Jarvis.Framework.Kernel.Engine;
using Jarvis.Framework.Shared.Events;
using Jarvis.Framework.Shared.IdentitySupport;
using Machine.Specifications;
using NStore.Domain;
using System.Collections.Generic;
using System.Linq;

namespace Jarvis.Framework.TestHelpers
{
    // ReSharper disable InconsistentNaming
    [Tags("aggregate")]
    public abstract class AggregateSpecification<TAggregate, TState, TId>
        where TAggregate : AggregateRoot<TState, TId>
        where TState : JarvisAggregateState, new()
        where TId : EventStoreIdentity
    {
        private static TAggregate _aggregate;

        protected AggregateSpecification()
        {
        }

        protected static TAggregate Aggregate
        {
            get
            {
                return _aggregate;
            }
        }

        protected static IEnumerable<DomainEvent> UncommittedEvents
        {
            get
            {
                var agg = (IEventSourcedAggregate)Aggregate;
                return agg.GetChangeSet().Events.Cast<DomainEvent>();
            }
        }

        protected static void ClearUncommittedEvents()
        {
            var agg = (IEventSourcedAggregate)Aggregate;
            agg.Persisted(agg.GetChangeSet());
        }

        protected static TState State
        {
            get { return (TState)_aggregate.GetPropertyValue("InternalState"); }
        }

        protected static void Create(IIdentity id)
        {
            _aggregate = TestAggregateFactory.Create<TAggregate, TId, TState>(new TState(), id, 0);
        }

        protected static void SetUp(TState state, IIdentity id)
        {
            _aggregate = TestAggregateFactory.Create<TAggregate, TId, TState>(state, id, 99999);
        }

        protected static bool EventHasBeenRaised<T>()
        {
            return UncommittedEvents.Any(x => x.GetType() == typeof(T));
        }

        protected static T RaisedEvent<T>() where T : DomainEvent
        {
            return (T)UncommittedEvents.Single(x => x.GetType() == typeof(T));
        }

        protected static IEnumerable<T> AllRaisedEvents<T>() where T : DomainEvent
        {
            return UncommittedEvents.OfType<T>();
        }
    }
}