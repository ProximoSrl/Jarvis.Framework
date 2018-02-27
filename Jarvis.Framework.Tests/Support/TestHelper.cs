using System;
using System.Collections.Generic;
using MongoDB.Driver;
using Jarvis.Framework.Shared.Helpers;
using System.Configuration;
using Jarvis.Framework.Shared.IdentitySupport;
using System.Reflection;
using Jarvis.Framework.Shared.IdentitySupport.Serialization;
using MongoDB.Bson.Serialization;
using NSubstitute;
using Jarvis.Framework.Kernel.Engine;
using NStore.Domain;
using NStore.Core.Logging;
using NStore.Core.Streams;
using NStore.Core.Persistence;
using System.Threading.Tasks;
using System.Linq;
using MongoDB.Bson;

namespace Jarvis.Framework.Tests.Support
{
    public static class TestHelper
    {
        public static IMongoDatabase CreateNew(string connectionString)
        {
            var url = new MongoUrl(connectionString);
            var client = new MongoClient(url);
            var db = client.GetDatabase(url.DatabaseName);

            db.Drop();

            return db;
        }

        public static IMongoDatabase GetEventStoreDb()
        {
            return CreateNew(ConfigurationManager.ConnectionStrings["eventstore"].ConnectionString);
        }

        public static async Task<List<Changeset>> EventStoreReadChangeset(this IPersistence persistence, String partitionId)
        {
            Recorder tape = new Recorder();
            var stream = new ReadOnlyStream(partitionId, persistence);
            await stream.ReadAsync(tape, 1).ConfigureAwait(false);
            return tape.Data.OfType<Changeset>().ToList();
        }

        public static Repository GetRepository()
        {
            var loggerFactory = Substitute.For<INStoreLoggerFactory>();
            loggerFactory.CreateLogger(Arg.Any<String>()).Returns(NStoreNullLogger.Instance);
            var persistence = new EventStoreFactoryTest(loggerFactory)
                .BuildEventStore(ConfigurationManager.ConnectionStrings["eventstore"].ConnectionString)
                .Result;
            return new Repository(new AggregateFactoryEx(null), new StreamsFactory(persistence));
        }

        private static IdentityManager _manager;
        private static HashSet<Type> _registeredTypes = new HashSet<Type>();

        public static void RegisterSerializerForFlatId<T>() where T : EventStoreIdentity
        {
            if (_manager == null)
            {
                var systemDb = TestHelper.CreateNew(ConfigurationManager.ConnectionStrings["system"].ConnectionString);
                _manager = new IdentityManager(new CounterService(systemDb));
                _manager.RegisterIdentitiesFromAssembly(Assembly.GetExecutingAssembly());
                MongoFlatIdSerializerHelper.Initialize(_manager);
            }
            if (!_registeredTypes.Contains(typeof(T)))
            {
                _registeredTypes.Add(typeof(T));
                BsonSerializer.RegisterSerializer(typeof(T), new TypedEventStoreIdentityBsonSerializer<T>());
                EventStoreIdentityCustomBsonTypeMapper.Register<T>();
            }
        }

        public static T BsonSerializeAndDeserialize<T>(this T obj) where T : class
        {
            if (obj == null)
                return null;
            var serialized = obj.ToBsonDocument();
            return BsonSerializer.Deserialize<T>(serialized);
        }

#if NETFULL
        public static void ClearAllQueue(params String[] queueList)
        {
            foreach (var queueName in queueList)
            {
                try
                {
                    var queue = new System.Messaging.MessageQueue(queueName);
                    queue.Purge();
                }
                catch
                {
                    // if the queue does not exists just ignore it
                }
            }
        }
#endif

        private static Int32 seed = 1;

        internal static IMongoDatabase GetTestDb()
        {
            var connectionStringBuilder = new MongoUrlBuilder(ConfigurationManager.ConnectionStrings["engine"].ConnectionString);
            connectionStringBuilder.DatabaseName += seed++.ToString();
            return connectionStringBuilder.ToString().CreateMongoDb();
        }

        public static IMongoDatabase CreateMongoDb(this String connectionString)
        {
            MongoUrl url = new MongoUrl(connectionString);
            var client = new MongoClient(url);
            return client.GetDatabase(url.DatabaseName);
        }
    }
}
