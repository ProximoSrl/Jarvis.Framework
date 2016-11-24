using Jarvis.Framework.Kernel.ProjectionEngine.Client;
using NEventStore.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jarvis.Framework.Kernel.ProjectionEngine.Client
{
	public interface ICommitPollingClientFactory
	{
		ICommitPollingClient Create(IPersistStreams persistStreams, String id);

		void Relese(ICommitPollingClient pollingClient);
	}
}
