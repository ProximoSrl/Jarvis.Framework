using System;
using Fasterflect;
using Jarvis.Framework.Kernel.Engine;
using Jarvis.NEventStoreEx.CommonDomainEx;

namespace Jarvis.Framework.TestHelpers
{
    public static class TestAggregateFactory
    {
        public static T Create<T, TState>(TState initialState, IIdentity identity, Int32 version) 
            where T : AggregateRoot
            where TState : AggregateState, new()
        {
            var ctor = typeof(T).Constructor(Flags.Default, new Type[] {  });
            if (ctor == null)
                throw new MissingMethodException(string.Format("{0} missing default ctor", typeof(T).FullName));

            var aggregate = (T)ctor.CreateInstance();
            AggregateSnapshot<TState> snapshot = new AggregateSnapshot<TState>()
            {
                Id = identity,
                State = initialState,
                Version = version
            };

            aggregate.AssignAggregateId(identity);
            ((ISnapshotable)aggregate).Restore(snapshot);

            return aggregate;
        }

        public static T Create<T, TState>(TState initialState, IIdentity identity)
            where T : AggregateRoot
            where TState : AggregateState, new()
        {
            return Create<T, TState>(initialState, identity, 0);
        }

        /// <summary>
        /// Create a new aggregate for test
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="TState"></typeparam>
        /// <param name="identity"></param>
        /// <returns></returns>
        public static T Create<T, TState>(IIdentity identity) 
            where T : AggregateRoot
            where TState : AggregateState, new()
        {
            return Create<T, TState>(new TState(), identity, 0);
        }
/*
        public static T Create<T>() where T : AggregateRoot
        {
            var ctor = typeof(T).Constructor(Flags.Default, new Type[] { });
            if (ctor == null)
                throw new MissingMethodException(string.Format("{0} missing default ctor",typeof(T).FullName));
            return (T)ctor.CreateInstance();
        }
        public static T Create<T>(IEnumerable<DomainEvent> events) where T : AggregateRoot
        {
            var aggregate = Create<T>();
            var iagg = (IAggregateEx)aggregate;
            foreach (var domainEvent in events)
            {
                iagg.ApplyEvent(domainEvent);
            }

            return aggregate;
        }
*/
    }
}