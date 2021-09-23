using NStore.Core.Persistence;
using System;
using System.Threading.Tasks;

namespace Jarvis.Framework.Kernel.ProjectionEngine.Client
{
    /// <summary>
    /// An abstractio nof a 
    /// </summary>
    public interface ICommitPollingClient
    {
        void Configure(
            Int64 checkpointTokenFrom,
            Int32 bufferSize);

        void Start();

        void Stop(Boolean setToFaulted);

        Task PollAsync();

        void AddConsumer(String consumerId, Func<IChunk, Task> consumerAction);
    }

    public enum CommitPollingClientStatus
    {
        Stopped,
        Polling,
        Faulted
    }
}