using Jarvis.Framework.Kernel.Store;
using Jarvis.NEventStoreEx.CommonDomainEx;

namespace Jarvis.Framework.Kernel.Engine
{
    public class AggregateSnapshot<TState> : IMementoEx where TState : AggregateState
    {
        public IIdentity Id { get; set; }
        public int Version { get; set; }
        public TState State { get; set; }
    }
}