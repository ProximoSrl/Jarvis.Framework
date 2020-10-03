using NStore.Core.Logging;
using NStore.Core.Persistence;
using NStore.Persistence.Mongo;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Jarvis.Framework.Tests.Support
{
    public class EventStoreFactoryTest
    {
        private readonly INStoreLoggerFactory _loggerFactory;

        /// <summary>
        /// Test Partition Collection Name
        /// </summary>
        public const String PartitionCollectionName = "Commits";

        public bool UseSyncDispatcher { get; set; }

        public EventStoreFactoryTest(INStoreLoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;
        }

        public async Task<IPersistence> BuildEventStore(
            string connectionString)
        {
            var mongoStoreOptions = new MongoPersistenceOptions
            {
                PartitionsConnectionString = connectionString,
                UseLocalSequence = true,
                PartitionsCollectionName = PartitionCollectionName,
                SequenceCollectionName = "event_sequence",
                DropOnInit = false
            };

            var mongoPersistence = new MongoPersistence(mongoStoreOptions);

            await mongoPersistence.InitAsync(CancellationToken.None).ConfigureAwait(false);

            return mongoPersistence;
        }
    }
}
