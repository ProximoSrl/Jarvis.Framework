//using NEventStore.Client;
//using NEventStore.Persistence;

//namespace Jarvis.Framework.Kernel.ProjectionEngine.Client
//{
//    public interface IPollingClient
//    {
//        /// <summary>
//        /// Observe commits from the sepecified checkpoint token. If the token is null,
//        /// all commits from the beginning will be observed.
//        /// </summary>
//        /// <param name="checkpointToken">The checkpoint token.</param>
//        /// <returns>
//        /// An <see cref="IObserveCommits" /> instance.
//        /// </returns>
//        IObserveCommits ObserveFrom(string checkpointToken = null);

//        void Create(IPersistStreams persistStreams, int interval);
//    }
//}