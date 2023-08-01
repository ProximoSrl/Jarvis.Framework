using MongoDB.Bson;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Jarvis.Framework.Shared.Support
{
    internal static class CollectionExtensionMethods
    {
        public static IReadOnlyCollection<string> GetIndexNames<T>(this IMongoCollection<T> collection)
        {
            if (!collection.Exists())
            {
                return Array.Empty<string>();
            }
            return collection.Indexes.List().ToList().Select(doc => doc["name"].AsString).ToList();
        }

        public static async Task<IReadOnlyCollection<string>> GetIndexNamesAsync<T>(this IMongoCollection<T> collection)
        {
            if (!collection.Exists())
            {
                return Array.Empty<string>();
            }

            var indexList = await collection.Indexes.ListAsync().ConfigureAwait(false);
            var indexListArray = await indexList.ToListAsync().ConfigureAwait(false);

            return indexListArray.Select(doc => doc["name"].AsString).ToList();
        }

        public static bool Exists<T>(this IMongoCollection<T> collection)
        {
            var client = collection.Database.Client;
            var database = client.GetDatabase(collection.Database.DatabaseNamespace.DatabaseName);
            var filter = new BsonDocument("name", collection.CollectionNamespace.CollectionName);
            var collections = database.ListCollections(new ListCollectionsOptions { Filter = filter });
            return collections.Any();
        }
    }
}
