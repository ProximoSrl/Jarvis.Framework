using System;
using System.Collections.Generic;
using System.Linq;
using Fasterflect;
using Jarvis.Framework.Kernel.Engine;
using Jarvis.Framework.Kernel.Store;
using Jarvis.Framework.Shared.Events;
using Jarvis.NEventStoreEx.CommonDomainEx;
using Machine.Specifications;

namespace Jarvis.Framework.TestHelpers
{
    // ReSharper disable InconsistentNaming
    [Tags("aggregate")]
    public abstract class AggregateSpecification<TAggregate, TState> 
        where TAggregate : AggregateRoot<TState>
        where TState : AggregateState, new()
    {
        private static TAggregate _aggregate;
        // ReSharper disable StaticFieldInGenericType
        private static IDisposable test_executions_context;
        private static string impersonatig_use_id = "prxm\\prxdev";
        // ReSharper restore StaticFieldInGenericType
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
                IAggregateEx agg = Aggregate;
                return agg.GetUncommittedEvents().Cast<DomainEvent>();
            }
        }

        protected static void ClearUncommittedEvents()
        {
            IAggregateEx agg = Aggregate;
            agg.ClearUncommittedEvents();
        }

        Establish context = () =>
        {
            _aggregate = null;
//            test_executions_context = new ImpersonatedContext(impersonatig_use_id);
        };

        private static TState _state;
        protected static TState State
        {
            get { return _state; }
        }

        protected static void Create(IIdentity id)
        {
            _state = new TState();
            _aggregate = TestAggregateFactory.Create<TAggregate, TState>(_state, id, 0);
        }

        protected static void SetUp(TState state, IIdentity id)
        {
            _state = state;
            _aggregate = TestAggregateFactory.Create<TAggregate, TState>(_state, id, 99999);
        }

        protected static void SetUp(TState state, TAggregate aggregate)
        {
            _state = state;
            _aggregate = aggregate;
        }

        private Cleanup stuff = () => test_executions_context.Dispose();

        protected static bool EventHasBeenRaised<T>()
        {
            return UncommittedEvents.Any(x => x.GetType() == typeof(T));
        }

        protected static T RaisedEvent<T>() where T:DomainEvent
        {
            return (T) UncommittedEvents.Single(x => x.GetType() == typeof (T));
        }

	    protected static IEnumerable<T> AllRaisedEvents<T>() where T : DomainEvent
	    {
			return UncommittedEvents.OfType<T>();
		}
    }
}