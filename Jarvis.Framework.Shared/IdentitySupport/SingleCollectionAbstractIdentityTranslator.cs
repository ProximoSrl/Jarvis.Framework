using Castle.Core.Logging;
using Jarvis.Framework.Shared.Exceptions;
using Jarvis.Framework.Shared.Helpers;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Jarvis.Framework.Shared.IdentitySupport
{
    /// <summary>
    /// A different version of <see cref="AbstractIdentityTranslator{TKey}" /> that uses mongodb
    /// but uses a single collection for all id, not a different collection for all id. This is
    /// needed to reduce number of collection used in the limitation of ATLAS M0/M2/M5 instances
    /// that have 500 collection limit.
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    public abstract class SingleCollectionAbstractIdentityTranslator<TKey> : IIdentityTranslator where TKey : EventStoreIdentity
    {
        private ILogger _logger;
        private readonly IMongoCollection<MappedIdentity> _collection;
        private IIdentityGenerator IdentityGenerator { get; set; }

        public ILogger Logger
        {
            get { return _logger ?? NullLogger.Instance; }
            set { _logger = value; }
        }

        /// <summary>
        /// This is the original mapped identity object of existing <see cref="AbstractIdentityTranslator{TKey}"/>
        /// used to migrate live.
        /// </summary>
        internal class MappedIdentity
        {
            /// <summary>
            /// Concatenation of external key and id type.
            /// </summary>
            [BsonId]
            internal string ExternalKey { get; private set; }

            [BsonElement]
            public string IdType { get; set; }

            [BsonElement]
            internal TKey AggregateId { get; private set; }

            public MappedIdentity(
                string externalKey,
                TKey aggregateId)
            {
                ExternalKey = externalKey;
                IdType = GetIdType();
                AggregateId = aggregateId;
            }

            public void ChangeKey(string newKey)
            {
                ExternalKey = newKey;
            }
        }

        private static string GetIdType()
        {
            return typeof(TKey).Name.ToLower();
        }

        protected SingleCollectionAbstractIdentityTranslator(IMongoDatabase systemDB, IIdentityGenerator identityGenerator)
        {
            IdentityGenerator = identityGenerator;
            _collection = systemDB.GetCollection<MappedIdentity>("mappers");

            var indexes = _collection.Indexes.List().ToList();
            var indexName = "idType";
            if (!indexes.Any(b => b["name"].AsString == indexName))
            {
                _collection.Indexes.CreateOne(new CreateIndexModel<MappedIdentity>(
                    Builders<MappedIdentity>.IndexKeys.Ascending(m => m.IdType),
                    new CreateIndexOptions()
                    {
                        Name = indexName,
                    }));
            }

            //we need to handle migrating from old collection to new single collection
            var originalCollection = systemDB.GetCollection<MappedIdentity>("map_" + typeof(TKey).Name.ToLower());
            //query to verify if we have element
            var data = originalCollection.AsQueryable().Count();
            if (data > 0)
            {
                //we can transfer data only if the new collection is empty
                var newElementCount = _collection.AsQueryable().Where(t => t.IdType == GetIdType()).Count();
                if (newElementCount > 0)
                {
                    throw new JarvisFrameworkEngineException($"Mapper {GetType().Name} found data in old collection {originalCollection.CollectionNamespace.FullName} but cannot transfer because in the single map collection there are already {newElementCount} element for type {GetIdType()}");
                }

                //ok we need to transfer everything to new collection, remember to change the id and add type
                var oldData = originalCollection.AsQueryable();
                // load at batch of 100 elements and move everyting to new collection
                var toSave = new List<MappedIdentity>();
                int currentPage = 0;
                int pageCount = 100;
                while (true)
                {
                    var page = oldData.Skip(currentPage * pageCount).Take(pageCount).ToList();
                    if (page.Count == 0)
                        break;

                    foreach (var mappedIdentity in page)
                    {
                        var newMappedIdentity = new MappedIdentity(GetId(mappedIdentity.ExternalKey), mappedIdentity.AggregateId);
                        toSave.Add(newMappedIdentity);
                    }

                    _collection.InsertMany(toSave);
                    currentPage++;
                    toSave.Clear();
                }

                //we need to delete / rename old collection
                systemDB.RenameCollection(originalCollection.CollectionNamespace.CollectionName, "old_" + originalCollection.CollectionNamespace.CollectionName);
            }
        }

        private string GetId(string alias)
        {
            return GetIdType() + "_" + alias;
        }

        /// <summary>
        /// Add an alias to a key, this is useful when you want
        /// multiple keys to map to the very same identity (TKey)
        /// </summary>
        /// <param name="key"></param>
        /// <param name="alias"></param>
        protected void AddAlias(TKey key, string alias)
        {
            if (alias == null) throw new ArgumentNullException(nameof(alias));
            alias = alias.ToLowerInvariant();

            var id = GetId(alias);
            var mapped = _collection.FindOneById(id);
            if (mapped != null)
            {
                if (mapped.AggregateId != key)
                    throw new JarvisFrameworkEngineException(string.Format("Alias {0} already mapped to {1}", alias, mapped.AggregateId));

                return;
            }

            MapIdentity(id, key);
        }

        protected void ReplaceAlias(TKey key, string alias)
        {
            if (alias == null) throw new ArgumentNullException(nameof(alias));
            alias = alias.ToLowerInvariant();
            var id = GetId(alias);
            DeleteAliases(key);
            MapIdentity(id, key);
        }

        /// <summary>
        /// Returns the first mapped value for a given key.
        /// </summary>
        /// <param name="key"></param>
        /// <exception cref="ArgumentNullException"></exception>
        protected String GetAlias(TKey key)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            var alias = _collection.Find(CreateFilterFromSingleKey(key)).FirstOrDefault();
            return alias?.ExternalKey;
        }

        private FilterDefinition<MappedIdentity> CreateFilterFromSingleKey(TKey key)
        {
            return Builders<MappedIdentity>
                .Filter
                .And(
                    Builders<MappedIdentity>.Filter.Eq("AggregateId", key.AsString()),
                    Builders<MappedIdentity>.Filter.Eq("IdType", GetIdType())
                );
        }

        /// <summary>
        /// Return reverse mapping for multiple aliases.
        /// Returns a Dictionary where the Keys are the requested keys and the value
        /// is the first mapped value (same behavior as <see cref="GetAlias(TKey)"/>
        /// </summary>
        /// <param name="keys"></param>
        /// <returns>Dictionary with mappings.</returns>
        protected IDictionary<TKey, String> GetAliases(IEnumerable<TKey> keys)
        {
            if (keys?.Any() != true)
                return new Dictionary<TKey, String>();

            return _collection
                .Find(CreateFilterForMultipleKeys(keys))
                .ToEnumerable()
                .GroupBy(k => k.AggregateId)
                .ToDictionary(_ => _.Key, _ => _.First().ExternalKey);
        }

        private static FilterDefinition<MappedIdentity> CreateFilterForMultipleKeys(IEnumerable<TKey> keys)
        {
            return Builders<MappedIdentity>
                .Filter
                    .And(
                        Builders<MappedIdentity>.Filter.In("AggregateId", keys),
                        Builders<MappedIdentity>.Filter.Eq("IdType", typeof(TKey).Name.ToLower())
                );
        }

        private static FilterDefinition<MappedIdentity> CreateFilterForMultipleKeys(IEnumerable<string> keys)
        {
            return Builders<MappedIdentity>
                .Filter
                    .And(
                        Builders<MappedIdentity>.Filter.In("AggregateId", keys),
                        Builders<MappedIdentity>.Filter.Eq("IdType", typeof(TKey).Name.ToLower())
                );
        }

        /// <summary>
        /// Returns all the aliases of a given key.
        /// </summary>
        /// <param name="key"></param>
        /// <exception cref="ArgumentNullException"></exception>
        protected string[] GetAliases(TKey key)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            var aliases = _collection.Find(CreateFilterFromSingleKey(key)).ToList();
            return aliases?.Select(a => a.ExternalKey).ToArray();
        }

        /// <summary>
        /// Return mapping for multiple keys, it does NOT create the mapping if it does not exists.
        /// </summary>
        /// <param name="externalKeys"></param>
        /// <returns>Dictionary with mappings.</returns>
        protected IDictionary<String, TKey> GetMultipleMapWithoutAutoCreation(IEnumerable<String> externalKeys)
        {
            if (externalKeys?.Any() != true)
                return new Dictionary<String, TKey>();

            return _collection
                .Find(CreateFilterForMultipleKeys(externalKeys))
                .ToEnumerable()
                .ToDictionary(_ => _.ExternalKey, _ => _.AggregateId, StringComparer.InvariantCultureIgnoreCase);
        }

        protected TKey Translate(string externalKey, bool createOnMissing = true)
        {
            if (IdentityGenerator == null) throw new JarvisFrameworkEngineException("Identity Generator not set");

            if (externalKey == null) throw new ArgumentNullException(nameof(externalKey));
            externalKey = externalKey.ToLowerInvariant();
            var id = GetId(externalKey);
            var mapped = _collection.FindOneById(id);

            if (mapped == null)
            {
                if (createOnMissing)
                    mapped = MapIdentity(id, IdentityGenerator.New<TKey>());
                else
                    throw new JarvisFrameworkEngineException(string.Format("No mapping found for key {0}:{1}", typeof(TKey).FullName, externalKey));
            }

            return mapped.AggregateId;
        }

        protected TKey TryTranslate(string externalKey)
        {
            if (IdentityGenerator == null) throw new JarvisFrameworkEngineException("Identity Generator not set");

            if (externalKey == null) throw new ArgumentNullException(nameof(externalKey));

            externalKey = externalKey.ToLowerInvariant();
            var id = GetId(externalKey);
            var mapped = _collection.FindOneById(id);

            return mapped?.AggregateId;
        }

        private MappedIdentity MapIdentity(string id, TKey key)
        {
            try
            {
                var mapped = new MappedIdentity(id, key);
                _collection.InsertOne(mapped);
                return mapped;
            }
            catch (Exception ex)
            {
                if (ex is MongoWriteException || ex is MongoWriteConcernException)
                {
                    if (ex.Message.Contains("E11000 duplicate key"))
                    {
                        var mapped = _collection.FindOneById(id);
                        if (mapped != null)
                            return mapped;
                    }
                }

                throw;
            }

            throw new JarvisFrameworkEngineException("Something went damn wrong...");
        }

        public void DeleteAliases(TKey key)
        {
            var id = key;
            _collection.DeleteMany(Builders<MappedIdentity>.Filter.Eq("AggregateId", id));
        }
    }
}
