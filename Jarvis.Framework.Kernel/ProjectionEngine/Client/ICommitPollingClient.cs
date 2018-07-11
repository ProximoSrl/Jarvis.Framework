using NStore.Core.Persistence;
using NStore.Persistence;
using System;
using System.Threading.Tasks;

namespace Jarvis.Framework.Kernel.ProjectionEngine.Client
{
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