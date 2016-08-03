using Jarvis.Framework.Kernel.Store;
using Jarvis.NEventStoreEx.CommonDomainEx;

namespace Jarvis.Framework.Kernel.Engine
{
    public interface ISnapshotable
    {
        void Restore(IMementoEx snapshot);
        IMementoEx GetSnapshot();
    }

}