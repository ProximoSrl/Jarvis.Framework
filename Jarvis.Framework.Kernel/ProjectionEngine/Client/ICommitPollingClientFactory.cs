using NStore.Core.Persistence;
using NStore.Persistence;
using System;

namespace Jarvis.Framework.Kernel.ProjectionEngine.Client
{
    public interface ICommitPollingClientFactory
	{
		ICommitPollingClient Create(IPersistence persistStreams, String id);

		void Relese(ICommitPollingClient pollingClient);
	}
}
