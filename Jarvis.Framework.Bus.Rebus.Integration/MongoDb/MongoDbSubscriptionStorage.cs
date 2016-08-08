using System;
using System.Linq;
using MongoDB.Bson;
using MongoDB.Driver;
using Rebus;

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

        /// <summary>
        /// Constructs the storage to persist subscriptions in the given collection, in the database specified by the connection string.
        /// </summary>
        public MongoDbSubscriptionStorage(string connectionString, string collectionName)
        {
            this.collectionName = collectionName;
            database = database = MongoHelper.GetDatabase(connectionString);
        }

        /// <summary>
        /// Adds the given subscriber input queue to the collection of endpoints listed as subscribers of the given event type
        /// </summary>
        public void Store(Type eventType, string subscriberInputQueue)
        {
            var collection = database.GetCollection<BsonDocument>(collectionName);

            var criteria = Builders<BsonDocument>.Filter.Eq("_id", eventType.FullName);
            var update = Builders<BsonDocument>.Update.AddToSet("endpoints", subscriberInputQueue);

            var safeModeResult = collection.WithWriteConcern(WriteConcern.Acknowledged).UpdateOne(
                criteria, 
                update, 
                new UpdateOptions() {
                        IsUpsert = true,
                });

            EnsureResultIsGood(safeModeResult, "Removing {0} from {1} where _id is {2}",
                             subscriberInputQueue, collectionName, eventType.FullName);
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

            EnsureResultIsGood(safeModeResult, "Removing {0} from {1} where _id is {2}",
                               subscriberInputQueue, collectionName, eventType.FullName);
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

        void EnsureResultIsGood(UpdateResult writeConcernResult, string message, params object[] objs)
        {
            if (writeConcernResult.ModifiedCount > 0)
            {
                throw new ApplicationException(
                    string.Format("The following operation didn't suceed: {0} - the result was: {1}",
                                  string.Format(message, objs),
                                  "ERROR"));
            }
        }
    }
}