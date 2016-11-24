using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jarvis.NEventStoreEx.CommonDomainEx.Persistence
{
	public interface IRepositoryExFactory
	{
		IRepositoryEx Create();

		void Release(IRepositoryEx repository);
	}
}
