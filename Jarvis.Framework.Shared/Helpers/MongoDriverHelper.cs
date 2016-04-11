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

        public static void Drop(this IMongoDatabase database)
        {
            database.Client.DropDatabase(database.DatabaseNamespace.DatabaseName);
        }

        public static T FindOneById<T>(this IMongoCollection<T> collection, Object idValue)
        {
            return collection.Find(Builders<T>.Filter.Eq("_id", idValue)).SingleOrDefault();
        }

        public static IFindFluent<T, T> FindAll<T>(this IMongoCollection<T> collection)
        {
            return collection.Find(Builders<T>.Filter.Empty);
        }

        public static void Save<T>(this IMongoCollection<T> collection, T objToSave, Object objectId)
        {
            collection.ReplaceOne(
                   Builders<T>.Filter.Eq("_id", objectId),
                   objToSave,
                   new UpdateOptions { IsUpsert = true });
        }

        public static DeleteResult Remove<T>(this IMongoCollection<T> collection, Object idValue)
        {
            return collection.DeleteOne(Builders<T>.Filter.Eq("_id", idValue));
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
