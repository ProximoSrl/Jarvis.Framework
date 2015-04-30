using NEventStore.Client;
using NEventStore.Persistence;

namespace Jarvis.Framework.Kernel.ProjectionEngine.Client
{
    public class PollingClientWrapperTpl : IPollingClient
    {
        JarvisPollingClientTpl _client;
        readonly ICommitEnhancer _enhancer;
        readonly bool _boost;

        public PollingClientWrapperTpl(ICommitEnhancer enhancer, bool boost)
        {
            _enhancer = enhancer;
            _boost = boost;
        }

        public void Create(IPersistStreams persistStreams, int interval)
        {
            _client = new JarvisPollingClientTpl(persistStreams,interval, _enhancer, _boost);
        }

        public IObserveCommits ObserveFrom(string checkpointToken = null)
        {
            return _client.ObserveFrom(checkpointToken);
        }
    }
}