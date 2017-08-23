using System;
using System.Threading.Tasks;
using NStore.Persistence.Mongo;
using System.Threading;
using NStore.Core.Logging;
using NStore.Core.Persistence;

namespace Jarvis.Framework.Kernel.Engine
{
	public class EventStoreFactory
    {
        private readonly INStoreLoggerFactory _loggerFactory;

        public const String PartitionCollectionName = "Commits";

        public bool UseSyncDispatcher { get; set; }

        public EventStoreFactory(INStoreLoggerFactory loggerFactory)
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
