using NStore.Core.Persistence;
using NStore.Domain;
using System;
using System.Threading.Tasks;

namespace Jarvis.Framework.Kernel.ProjectionEngine.Client
{
    /// <summary>
    /// This  interface abstract the concept of a poller that is capable of
    /// continuously polling some event source to feed a projection engine.
    /// </summary>
    public interface ICommitPollingClient
    {
        void Configure(
            Int64 checkpointTokenFrom,
            Int32 bufferSize);

        void Start();

        void Stop(Boolean setToFaulted);

        Task PollAsync();

        /// <summary>
        /// Add a consumer function that will be used to dispatch the
        /// chunk read from the storage
        /// </summary>
        /// <param name="consumerAction"></param>
        void AddConsumer(String consumerId, Func<IChunk, Task> consumerAction);
    }

    public enum CommitPollingClientStatus
    {
        Stopped,
        Polling,
        Faulted
    }
}