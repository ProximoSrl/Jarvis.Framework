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
    public interface IIdentityTranslator
    {
    }

    public abstract class AbstractIdentityTranslator<TKey> : IIdentityTranslator where TKey : EventStoreIdentity
    {
        private ILogger _logger;
        private readonly IMongoCollection<MappedIdentity> _collection;
        private IIdentityGenerator IdentityGenerator { get; set; }

        public ILogger Logger
        {
            get { return _logger ?? NullLogger.Instance; }
            set { _logger = value; }
        }

        internal class MappedIdentity
        {
            [BsonId]
            internal string ExternalKey { get; private set; }

            [BsonElement]
            internal TKey AggregateId { get; private set; }

            public MappedIdentity(string externalKey, TKey aggregateId)
            {
                ExternalKey = externalKey;
                AggregateId = aggregateId;
            }

            public void ChangeKey(string newKey)
            {
                ExternalKey = newKey;
            }
        }

        protected AbstractIdentityTranslator(IMongoDatabase systemDB, IIdentityGenerator identityGenerator)
        {
            IdentityGenerator = identityGenerator;
            _collection = systemDB.GetCollection<MappedIdentity>("map_" + typeof(TKey).Name.ToLower());
        }

        /// <summary>
        /// Add an aliast to a key, this is useful when you want 
        /// multiple keys to map to the very same identity (TKey)
        /// </summary>
        /// <param name="key"></param>
        /// <param name="alias"></param>
        protected void AddAlias(TKey key, string alias)
        {
            if (alias == null) throw new ArgumentNullException(nameof(alias));
            alias = alias.ToLowerInvariant();

            var mapped = _collection.FindOneById(alias);
            if (mapped != null)
            {
                if (mapped.AggregateId != key)
                    throw new JarvisFrameworkEngineException(string.Format("Alias {0} already mapped to {1}", alias, mapped.AggregateId));

                return;
            }

            MapIdentity(alias, key);
        }

        protected void ReplaceAlias(TKey key, string alias)
        {
            if (alias == null) throw new ArgumentNullException(nameof(alias));
            alias = alias.ToLowerInvariant();
            DeleteAliases(key);
            MapIdentity(alias, key);
        }

        protected String GetAlias(TKey key)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            var alias = _collection.Find(Builders<MappedIdentity>.Filter.Eq("AggregateId", key.AsString())).FirstOrDefault();
            return alias?.ExternalKey;
        }

        /// <summary>
        /// Return reverse mapping for multiple aliases.
        /// </summary>
        /// <param name="keys"></param>
        /// <returns>Dictionary with mappings.</returns>
        protected IDictionary<TKey, String> GetAliases(IEnumerable<TKey> keys)
        {
            if (keys == null || !keys.Any())
                return new Dictionary<TKey, String>();

            return _collection
                .Find(Builders<MappedIdentity>.Filter.In("AggregateId", keys))
                .ToEnumerable()
                .ToDictionary(_ => _.AggregateId, _ => _.ExternalKey);
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
                .Find(Builders<MappedIdentity>.Filter.In("ExternalKey", externalKeys.Select(_ => _.ToLowerInvariant())))
                .ToEnumerable()
                .ToDictionary(_ => _.ExternalKey, _ => _.AggregateId, StringComparer.InvariantCultureIgnoreCase);
        }

        protected TKey Translate(string externalKey, bool createOnMissing = true)
        {
            if (IdentityGenerator == null) throw new JarvisFrameworkEngineException("Identity Generator not set");

            if (externalKey == null) throw new ArgumentNullException(nameof(externalKey));
            externalKey = externalKey.ToLowerInvariant();
            var mapped = _collection.FindOneById(externalKey);

            if (mapped == null)
            {
                if (createOnMissing)
                    mapped = MapIdentity(externalKey, IdentityGenerator.New<TKey>());
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
            var mapped = _collection.FindOneById(externalKey);

            return mapped == null ? null : mapped.AggregateId;
        }

        private MappedIdentity MapIdentity(string externalKey, TKey key)
        {
            try
            {
                var mapped = new MappedIdentity(externalKey, key);
                _collection.InsertOne(mapped);
                return mapped;
            }
            catch (Exception ex)
            {
                if (ex is MongoWriteException || ex is MongoWriteConcernException)
                {
                    if (ex.Message.Contains("E11000 duplicate key"))
                    {
                        var mapped = _collection.FindOneById(externalKey);
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
