using NStore.Core.Persistence;
using NStore.Persistence;
using System;

namespace Jarvis.Framework.Kernel.ProjectionEngine.Client
{
    /// <summary>
    /// Needed to create <see cref="ICommitPollingClient"/> instances on the 
    /// fly from projection engines.
    /// </summary>
    public interface ICommitPollingClientFactory
	{
		ICommitPollingClient Create(IPersistence persistStreams, String id);

        /// <summary>
        /// This will release the instance.
        /// </summary>
        /// <param name="pollingClient"></param>
		void Release(ICommitPollingClient pollingClient);
	}
}
