
using NStore.Core.Persistence;

namespace Jarvis.Framework.Kernel.Engine.Projections
{
    public interface IHousekeeper
    {
        void Init();
        void RemoveAll(IPersistence advanced);
    }
}