using NStore.Core.Persistence;
using NStore.Persistence;
using System.Threading.Tasks;

namespace Jarvis.Framework.Kernel.ProjectionEngine
{
    public interface IHousekeeper
    {
		Task InitAsync();
		Task RemoveAllAsync(IPersistence advanced);
    }

    public class NullHouseKeeper : IHousekeeper
    {
        public Task InitAsync()
        {
			return Task.CompletedTask;
        }

        public Task RemoveAllAsync(IPersistence advanced)
        {
			return Task.CompletedTask;
		}
    }
}