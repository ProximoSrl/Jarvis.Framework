using System;
using NEventStore;
using System.Threading.Tasks;

namespace Jarvis.Framework.Kernel.ProjectionEngine.Client
{
    public interface ICommitPollingClient
    {
        void StartAutomaticPolling(
            Int64 checkpointTokenFrom,
            Int32 intervalInMilliseconds,
            Int64 checkpointTokenSequenced,
            Int32 bufferSize,
            String pollerName);

        void Poll();

        void StartManualPolling(
            Int64 checkpointTokenFrom,
            Int32 intervalInMilliseconds,
            Int32 bufferSize,
            String pollerName);

        void AddConsumer(Func<ICommit, Task> consumerAction);

        void StopPolling(Boolean setToFaulted);
    }

    public enum CommitPollingClientStatus
    {
        Stopped,
        Polling,
        Faulted
    }
}