using System;
using System.Linq;
using MongoDB.Bson;
using MongoDB.Driver;
using Rebus;
using Castle.Core.Logging;

namespace Jarvis.Framework.Bus.Rebus.Integration.MongoDb
{
    /// <summary>
    /// MongoDB implementation of Rebus' <see cref="IStoreSubscriptions"/>. Will store subscriptions in one document per
    /// logical event type, keeping an array of subscriber endpoints inside that document. The document _id is
    /// the full name of the event type.
    /// </summary>
    public class MongoDbSubscriptionStorage : IStoreSubscriptions
    {
        readonly string collectionName;
        readonly IMongoDatabase database;
        ILogger logger;

        /// <summary>
        /// Constructs the storage to persist subscriptions in the given collection, in the database specified by the connection string.
        /// </summary>
        public MongoDbSubscriptionStorage(
            string connectionString,
            string collectionName,
            ILogger logger)
        {
            this.collectionName = collectionName;
            this.database = MongoHelper.GetDatabase(connectionString);
            this.logger = logger;
        }

        /// <summary>
        /// Executes an action that involve writing in MongoDb and retry if there are
        /// Write Exception due to concurrency.
        /// </summary>
        /// <param name="action"></param>
        /// <param name="message">A descriptive message of the operation.</param>
        private void ExecuteWithRetry(Action action, String message)
        {
            Int32 count = 0;
            do
            {
                try
                {
                    action();
                    return;
                }
                catch (MongoWriteException wex)
                {
                    this.logger.DebugFormat(wex, "Error in executing action {0}", message);
                }
            } while (count++ < 3);
        }

        /// <summary>
        /// Adds the given subscriber input queue to the collection of endpoints listed as subscribers of the given event type
        /// </summary>
        public void Store(Type eventType, string subscriberInputQueue)
        {
            var collection = database.GetCollection<BsonDocument>(collectionName);

            var criteria = Builders<BsonDocument>.Filter.Eq("_id", eventType.FullName);
            var update = Builders<BsonDocument>.Update.AddToSet("endpoints", subscriberInputQueue);
            //Sometimes registration are done probably with multiple threads, and the insert fails
            //due to index violation. We can safely ignore that error
            try
            {
                var description = String.Format("Error registering subscription for event type {0} in queue {1}",
                    eventType.FullName, subscriberInputQueue);
                ExecuteWithRetry(
                    () => PersistRegistration(eventType, subscriberInputQueue, collection, criteria, update),
                    description);
            }
            catch (Exception ex)
            {
                this.logger.ErrorFormat(ex, "Error registering subscription for event type {0} in queue {1}",
                    eventType.FullName, subscriberInputQueue);
                throw;
            }
        }

        private void PersistRegistration(Type eventType, string subscriberInputQueue, IMongoCollection<BsonDocument> collection, FilterDefinition<BsonDocument> criteria, UpdateDefinition<BsonDocument> update)
        {
            var safeModeResult = collection.WithWriteConcern(WriteConcern.Acknowledged).UpdateOne(
                               criteria,
                               update,
                               new UpdateOptions()
                               {
                                   IsUpsert = true,
                               });
        }

        /// <summary>
        /// Removes the given subscriber from the collection of endpoints listed as subscribers of the given event type
        /// </summary>
        public void Remove(Type eventType, string subscriberInputQueue)
        {
            var collection = database.GetCollection<BsonDocument>(collectionName);

            var criteria = Builders<BsonDocument>.Filter.Eq("_id", eventType.FullName);
            var update = Builders<BsonDocument>.Update.Pull("endpoints", subscriberInputQueue);

            var safeModeResult = collection.WithWriteConcern(WriteConcern.Acknowledged).UpdateOne(
             criteria,
             update,
             new UpdateOptions()
             {
                 IsUpsert = true,
             });
        }

        /// <summary>
        /// Gets all subscriber for the given event type
        /// </summary>
        public string[] GetSubscribers(Type eventType)
        {
            var collection = database.GetCollection<BsonDocument>(collectionName);

            var doc = collection.Find(Builders<BsonDocument>.Filter.Eq("_id", eventType.FullName)).SingleOrDefault();
            if (doc == null) return new string[0];

            var bsonDocument = doc.AsBsonDocument;
            if (bsonDocument == null) return new string[0];

            var endpoints = bsonDocument["endpoints"].AsBsonArray;
            return endpoints.Values.Select(v => v.ToString()).ToArray();
        }
    }
}