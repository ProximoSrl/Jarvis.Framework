using Castle.Windsor;
using NStore.Domain;

namespace Jarvis.Framework.Shared.Persistence
{
	public interface IRepositoryFactory
	{
		IRepository Create();

		void Release(IRepository repository);
	}
}
