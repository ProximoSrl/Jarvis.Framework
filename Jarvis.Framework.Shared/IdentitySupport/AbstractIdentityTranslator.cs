using System;
using Castle.Core.Logging;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using Jarvis.Framework.Shared.Helpers;
using MongoDB.Bson.Serialization;
using Jarvis.Framework.Shared.IdentitySupport.Serialization;

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
        internal Boolean IsFlatMapping { get; private set; }
        protected AbstractIdentityTranslator(IMongoDatabase systemDB, IIdentityGenerator identityGenerator)
        {
            IdentityGenerator = identityGenerator;
            _collection = systemDB.GetCollection<MappedIdentity>("map_" + typeof(TKey).Name.ToLower());
            var serializer = BsonSerializer.LookupSerializer(typeof(TKey));
            if (serializer is EventStoreIdentityBsonSerializer)
            {
                IsFlatMapping = true;
            }
        }

        protected void AddAlias(TKey key, string alias)
        {
            if (alias == null) throw new ArgumentNullException("alias");
            alias = alias.ToLowerInvariant();

            var mapped = _collection.FindOneById(alias);
            if (mapped != null)
            {
                if (mapped.AggregateId != key)
                    throw new Exception(string.Format("Alias {0} already mapped to {1}", alias, mapped.AggregateId));

                return;
            }

            MapIdentity(alias, key);
        }

        protected void ReplaceAlias(TKey key, string alias)
        {
            if (alias == null) throw new ArgumentNullException();
            alias = alias.ToLowerInvariant();
            _collection.Remove(Builders<MappedIdentity>.Filter.Eq(f => f.AggregateId, key.FullyQualifiedId));
            MapIdentity(alias, key);
        }

        protected TKey Translate(string externalKey, bool createOnMissing = true)
        {
            if (IdentityGenerator == null) throw new Exception("Identity Generator not set");

            if (externalKey == null) throw new ArgumentNullException("externalKey");
            externalKey = externalKey.ToLowerInvariant();
            var mapped = _collection.FindOneById(externalKey);

            if (mapped == null)
            {
                if (createOnMissing)
                    mapped = MapIdentity(externalKey, IdentityGenerator.New<TKey>());
                else
                    throw new Exception(string.Format("No mapping found for key {0}:{1}", typeof(TKey).FullName, externalKey));
            }

            return mapped.AggregateId;
        }

        protected TKey TryTranslate(string externalKey)
        {
            if (IdentityGenerator == null) throw new Exception("Identity Generator not set");

            if (externalKey == null) throw new ArgumentNullException("externalKey");

            externalKey = externalKey.ToLowerInvariant();
            var mapped = _collection.FindOneById(externalKey);

            return mapped == null ? null : mapped.AggregateId;
        }

        protected TKey LinkKeys(string externalKey, TKey key)
        {
            return MapIdentity(externalKey, key).AggregateId;
        }

        private MappedIdentity MapIdentity(string externalKey, TKey key)
        {
            try
            {
                var mapped = new MappedIdentity(externalKey, key);
                _collection.InsertOne(mapped);
                return mapped;
            }
            catch (MongoWriteConcernException ex)
            {
                if (ex.Message.Contains("E11000 duplicate key"))
                {
                    var mapped = _collection.FindOneById(externalKey);
                    if (mapped != null)
                        return mapped;
                }
                else
                {
                    throw;
                }
            }

            throw new Exception("Something went damn wrong...");
        }

        public void DeleteAliases(TKey key)
        {
            _collection.Remove(Builders<MappedIdentity>.Filter.Eq(f => f.AggregateId, key.FullyQualifiedId));
        }
    }
}
