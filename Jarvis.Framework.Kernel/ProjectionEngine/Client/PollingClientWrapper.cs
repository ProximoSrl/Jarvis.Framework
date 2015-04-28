using NEventStore.Client;
using NEventStore.Persistence;

namespace Jarvis.Framework.Kernel.ProjectionEngine.Client
{
    public class PollingClientWrapper : IPollingClient
    {
        JarvisPollingClient _client;
        readonly CommitEnhancer _enhancer;
        readonly bool _boost;
        private IConcurrentCheckpointTracker _tracker;

        public PollingClientWrapper(CommitEnhancer enhancer, bool boost, IConcurrentCheckpointTracker tracker)
        {
            _enhancer = enhancer;
            _boost = boost;
            _tracker = tracker;
        }

        public void Create(IPersistStreams persistStreams, int interval)
        {
            _client = new JarvisPollingClient(persistStreams, interval, _enhancer, _boost, _tracker);
        }

        public IObserveCommits ObserveFrom(string checkpointToken = null)
        {
            return _client.ObserveFrom(checkpointToken);
        }
    }
}