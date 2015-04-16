using NEventStore.Persistence;

namespace CQRS.Kernel.Engine.Projections
{
    public interface IHousekeeper
    {
        void Init();
        void RemoveAll(IPersistStreams advanced);
    }
}