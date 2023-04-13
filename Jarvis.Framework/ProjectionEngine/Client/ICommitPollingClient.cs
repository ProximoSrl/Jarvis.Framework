using NStore.Core.Persistence;
using System;
using System.Threading.Tasks;

namespace Jarvis.Framework.Kernel.ProjectionEngine.Client
{
    /// <summary>
    /// An abstraction of a component that polls NStore commits.
    /// </summary>
    public interface ICommitPollingClient
    {
        /// <summary>
        /// Configure polling client telling the starting point and how many
        /// commit it can keep in memory in the TPL chain (essentially is the
        /// backpressure of the poller)
        /// </summary>
        /// <param name="checkpointTokenFrom"></param>
        /// <param name="bufferSize"></param>
        void Configure(
            Int64 checkpointTokenFrom,
            Int32 bufferSize);

        /// <summary>
        /// Start automatic polling, this method can specify frequence of the poller
        /// and the polling starts polling immediately.
        /// </summary>
        void Start(int frequenceInMilliseconds);

        /// <summary>
        /// Stop the polller, and tells if the stop needs to set poller to faulted, this
        /// happens if external code detects that something is wrong, no more data must be polled
        /// everything must be stopped and signal with an health check that something is wrong.
        /// </summary>
        /// <param name="setToFaulted"></param>
        void Stop(Boolean setToFaulted);

        /// <summary>
        /// Manual polling, this is extremely useful if you have MongoDb change stream notifications
        /// so you can limit polling to a really high frequency (1 minute) and trigger whenever a real
        /// change happens.
        /// </summary>
        /// <returns></returns>
        Task PollAsync();

        /// <summary>
        /// Add another consumer to polling client, consumers are the action that will be executed
        /// when another commit is polled from the database.
        /// </summary>
        /// <param name="consumerId"></param>
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