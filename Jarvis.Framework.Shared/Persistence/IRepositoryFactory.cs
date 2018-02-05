using Castle.Windsor;
using NStore.Domain;

namespace Jarvis.Framework.Shared.Persistence
{
	/// <summary>
	/// Standard factory for repository, where you can create
	/// a repository from the Castle Windsor container and then
	/// release once the usage of the repository is finished.
	/// </summary>
	public interface IRepositoryFactory
	{
		IRepository Create();

		void Release(IRepository repository);
	}
}
