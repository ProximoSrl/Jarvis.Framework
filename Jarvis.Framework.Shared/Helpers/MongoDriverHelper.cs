using Jarvis.Framework.Shared.Exceptions;
using Jarvis.Framework.Shared.Support;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using MongoDB.Driver.Core.Clusters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Jarvis.Framework.Shared.Helpers
{
    public static class MongoDriverHelper
    {
        public static void Drop<T>(this IMongoCollection<T> collection)
        {
            collection.Database.DropCollection(collection.CollectionNamespace.CollectionName);
        }

        public static async Task DropAsync<T>(this IMongoCollection<T> collection)
        {
            await collection.Database.DropCollectionAsync(collection.CollectionNamespace.CollectionName).ConfigureAwait(false);
        }

        public static void Drop(this IMongoDatabase database)
        {
            database.Client.DropDatabase(database.DatabaseNamespace.DatabaseName);
        }

        public static T FindOneById<T, Tid>(this IMongoCollection<T> collection, Tid idValue)
        {
            return collection.Find(Builders<T>.Filter.Eq("_id", idValue)).SingleOrDefault();
        }

        public static async Task<T> FindOneByIdAsync<T, Tid>(this IMongoCollection<T> collection, Tid idValue)
        {
            var finder = await collection.FindAsync(Builders<T>.Filter.Eq("_id", idValue)).ConfigureAwait(false);
            return await finder.SingleOrDefaultAsync().ConfigureAwait(false);
        }

        public static T FindOneById<T>(this IMongoCollection<T> collection, BsonValue idValue)
        {
            var value = BsonTypeMapper.MapToDotNetValue(idValue);
            if (idValue == null)
            {
                throw new JarvisFrameworkEngineException("FindOneById wrapper needs not a BsonValue but a plain value to be specified");
            }

            return collection.Find(Builders<T>.Filter.Eq("_id", value)).SingleOrDefault();
        }

        public static IFindFluent<T, T> FindAll<T>(this IMongoCollection<T> collection)
        {
            return collection.Find(Builders<T>.Filter.Empty);
        }

        public static void SaveWithGeneratedObjectId<T>(this IMongoCollection<T> collection, T objToSave, Action<ObjectId> idAssigner)
        {
            var id = ObjectId.GenerateNewId();
            idAssigner(id);
            collection.ReplaceOne(
                   Builders<T>.Filter.Eq("_id", id),
                   objToSave,
                   new UpdateOptions { IsUpsert = true });
        }

        public static void Save<T, Tid>(this IMongoCollection<T> collection, T objToSave, Tid objectId)
        {
            if (ObjectId.Empty.Equals(objectId))
                throw new ArgumentException("Cannot save with null objectId", nameof(objectId));
            collection.ReplaceOne(
                   Builders<T>.Filter.Eq("_id", objectId),
                   objToSave,
                   new UpdateOptions { IsUpsert = true });
        }

        public static Task SaveAsync<T, Tid>(this IMongoCollection<T> collection, T objToSave, Tid objectId)
        {
            if (ObjectId.Empty.Equals(objectId))
                throw new ArgumentException("Cannot save with null objectId", nameof(objectId));
            return collection.ReplaceOneAsync(
                   Builders<T>.Filter.Eq("_id", objectId),
                   objToSave,
                   new UpdateOptions { IsUpsert = true });
        }

        public static DeleteResult RemoveById<T, Tid>(this IMongoCollection<T> collection, Tid idValue)
        {
            return collection.DeleteOne(Builders<T>.Filter.Eq("_id", idValue));
        }

        public static Task<DeleteResult> RemoveByIdAsync<T, Tid>(this IMongoCollection<T> collection, Tid idValue)
        {
            return collection.DeleteOneAsync(Builders<T>.Filter.Eq("_id", idValue));
        }

        public static Dictionary<Tkey, Tvalue> AsDictionary<Tkey, Tvalue>(this BsonValue bsonValue)
        {
            using (BsonReader reader = new JsonReader(bsonValue.ToJson()))
            {
                var dictionarySerializer = new DictionaryInterfaceImplementerSerializer<Dictionary<Tkey, Tvalue>>();
                object result = dictionarySerializer.Deserialize(
                    BsonDeserializationContext.CreateRoot(reader, b => { }),
                    new BsonDeserializationArgs()
                    {
                        NominalType = typeof(Dictionary<Tkey, Tvalue>)
                    }
                );
                return (Dictionary<Tkey, Tvalue>)result;
            }
        }

        public static async Task<Boolean> IndexExistsAsync<T>(this IMongoCollection<T> collection, String indexName)
        {
            var indexesQuery = await collection.Indexes.ListAsync().ConfigureAwait(false);
            var indexes = await indexesQuery.ToListAsync().ConfigureAwait(false);
            return indexes.Any(x => x["name"].AsString == indexName);
        }

        /// <summary>
        /// Check connection and returns true if the instance of mongodb is operational.
        /// </summary>
        /// <param name="connection"></param>
        /// <returns></returns>
        public static Boolean CheckConnection(String connection)
        {
            var url = new MongoUrl(connection);
            var client = url.CreateClient(false);
            return CheckConnection(client);
        }

        /// <summary>
        /// Simple class used to check if connection with mongo is ok or if the 
        /// cluster cannot be contacted.
        /// </summary>
        /// <param name="client"></param>
        /// <returns></returns>
        public static bool CheckConnection(IMongoClient client)
        {
            Task.Factory.StartNew(() => client.ListDatabases()); //forces a database connection
            Int32 spinCount = 0;
            ClusterState clusterState;

            while ((clusterState = client.Cluster.Description.State) != ClusterState.Connected
                && spinCount++ < 100)
            {
                Thread.Sleep(20);
            }
            return clusterState == ClusterState.Connected;
        }
    }
}
