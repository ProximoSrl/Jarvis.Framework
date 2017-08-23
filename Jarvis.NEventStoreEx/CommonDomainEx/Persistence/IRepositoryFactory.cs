using NStore.Aggregates;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jarvis.NEventStoreEx.CommonDomainEx.Persistence
{
	public interface IRepositoryFactory
	{
		IRepository Create();

		void Release(IRepository repository);
	}
}
