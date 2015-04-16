using NEventStore.Persistence;

namespace Jarvis.Framework.Kernel.ProjectionEngine
{
    public interface IHousekeeper
    {
        void Init();
        void RemoveAll(IPersistStreams advanced);
    }

    public class NullHouseKeeper : IHousekeeper
    {
        public void Init()
        {

        }

        public void RemoveAll(IPersistStreams advanced)
        {
        }
    }
}