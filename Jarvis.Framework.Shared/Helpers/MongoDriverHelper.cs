using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using MongoDB.Driver.Core.Operations;
using MongoDB.Driver.Core.WireProtocol.Messages.Encoders;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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

        public static T FindOneById<T, Tid>(this IMongoCollection<T> collection, BsonValue idValue)
        {

            var value = BsonTypeMapper.MapToDotNetValue(idValue);
            if (idValue == null)
            {
                throw new ApplicationException("FindOneById wrapper needs not a BsonValue but a plain value to be specified");
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
                throw new ApplicationException("Cannot save with null objectId");
            var result = collection.ReplaceOne(
                   Builders<T>.Filter.Eq("_id", objectId),
                   objToSave,
                   new UpdateOptions { IsUpsert = true });
        }

        public static async Task SaveAsync<T, Tid>(this IMongoCollection<T> collection, T objToSave, Tid objectId)
        {
            if (ObjectId.Empty.Equals(objectId))
                throw new ApplicationException("Cannot save with null objectId");
            await collection.ReplaceOneAsync(
                   Builders<T>.Filter.Eq("_id", objectId),
                   objToSave,
                   new UpdateOptions { IsUpsert = true }).ConfigureAwait(false);
        }

        public static DeleteResult RemoveById<T, Tid>(this IMongoCollection<T> collection, Tid idValue)
        {
            return collection.DeleteOne(Builders<T>.Filter.Eq("_id", idValue));
        }

        public static async Task<DeleteResult> RemoveByIdAsync<T, Tid>(this IMongoCollection<T> collection, Tid idValue)
        {
            return await collection.DeleteOneAsync(Builders<T>.Filter.Eq("_id", idValue)).ConfigureAwait(false);
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
    }

}
