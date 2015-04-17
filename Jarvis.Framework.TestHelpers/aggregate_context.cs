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
        private static List<IDisposable> _disposeOnCleanup = new List<IDisposable>();
        private static TAggregate aggregate;
        protected static TAggregate Aggregate
        {
            get
            {
                return aggregate;
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

        Establish context = () =>
        {
            aggregate = null;
        };

        private static TState _State;
        protected static TState State
        {
            get { return _State; }
        }

        protected static void Create(IIdentity id)
        {
            _State = new TState();
            aggregate = TestAggregateFactory.Create<TAggregate, TState>(_State, id, 0);
        }

        protected static void SetUp(TState state, IIdentity id)
        {
            _State = state;
            aggregate = TestAggregateFactory.Create<TAggregate, TState>(_State, id, 99999);
        }

        private Cleanup stuff = () =>
        {
            foreach (var disposable in _disposeOnCleanup)
            {
                disposable.Dispose();
            }
            
            _disposeOnCleanup.Clear();
        };

        protected void RegisterForCleanup(IDisposable disposable)
        {
            _disposeOnCleanup.Add(disposable);
        }

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