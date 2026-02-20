using Castle.Core.Logging;
using Jarvis.Framework.Shared.Exceptions;
using Jarvis.Framework.Shared.Helpers;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

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

        protected internal class MappedIdentity
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
        /// Add an alias to a key, this is useful when you want
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

        /// <summary>
        /// Returns the first mapped value for a given key.
        /// </summary>
        /// <param name="key"></param>
        /// <exception cref="ArgumentNullException"></exception>
        protected String GetAlias(TKey key)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            var alias = _collection.Find(Builders<MappedIdentity>.Filter.Eq("AggregateId", key.AsString())).FirstOrDefault();
            return alias?.ExternalKey;
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
                .Find(Builders<MappedIdentity>.Filter.In("AggregateId", keys))
                .ToEnumerable()
                .GroupBy(k => k.AggregateId)
                .ToDictionary(_ => _.Key, _ => _.First().ExternalKey);
        }

        /// <summary>
        /// Returns all the aliases of a given key.
        /// </summary>
        /// <param name="key"></param>
        /// <exception cref="ArgumentNullException"></exception>
        protected string[] GetAliases(TKey key)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            var aliases = _collection.Find(Builders<MappedIdentity>.Filter.Eq("AggregateId", key.AsString())).ToList();
            return aliases?.Select(a => a.ExternalKey).ToArray();
        }

        /// <summary>
        /// Return mapping for multiple keys, it does NOT create the mapping if it does not exists.
        /// </summary>
        /// <param name="externalKeys"></param>
        /// <returns>Dictionary with mappings.</returns>
        protected IReadOnlyDictionary<String, TKey> GetMultipleMapWithoutAutoCreation(IEnumerable<String> externalKeys)
        {
            if (externalKeys?.Any() != true)
            {
                return ReadOnlyDictionary<String, TKey>.Empty;
            }

            var lowercaseInvariantKeys = externalKeys.Select(_ => _.ToLowerInvariant()).ToList();
            return _collection
                .Find(Builders<MappedIdentity>.Filter.In("ExternalKey", lowercaseInvariantKeys))
                .ToEnumerable()
                .ToDictionary(_ => _.ExternalKey, _ => _.AggregateId, StringComparer.InvariantCultureIgnoreCase);
        }

        /// <summary>
        /// Translate multiple external keys to identities with optional auto-creation.
        /// Uses a single database call to fetch existing mappings, then creates missing ones if requested.
        /// </summary>
        /// <param name="externalKeys">The external keys to translate.</param>
        /// <param name="createOnMissing">If true, creates mappings for keys that don't exist.</param>
        /// <param name="cancellationToken"></param>
        /// <returns>Dictionary mapping external keys to identities.</returns>
        protected async Task<IReadOnlyDictionary<String, TKey>> TranslateBatchAsync(
            IEnumerable<String> externalKeys, 
            bool createOnMissing,
            CancellationToken cancellationToken = default)
        {
            if (externalKeys?.Any() != true)
            {
                return ReadOnlyDictionary<String, TKey>.Empty;
            }

            // Normalize all keys to lowercase
            var normalizedKeys = externalKeys
                .Select(k => k.ToLowerInvariant())
                .ToList();
                
            // Fetch existing mappings in a single query
            var returnMapping = _collection
                .Find(Builders<MappedIdentity>.Filter.In("ExternalKey", normalizedKeys))
                .ToEnumerable()
                .ToDictionary(_ => _.ExternalKey, _ => _.AggregateId, StringComparer.InvariantCultureIgnoreCase);

            // If we don't need to create missing keys, return what we found
            if (!createOnMissing) 
            {
                return returnMapping;
            }

            // Find missing keys
            var missingKeys = normalizedKeys
                .Where(k => !returnMapping.ContainsKey(k))
                .ToList();

            if (missingKeys.Count == 0)
                return returnMapping;

            // Create new mappings for missing keys but first of all generate all new identities
            var identities = await IdentityGenerator.NewManyAsync<TKey>(missingKeys.Count, cancellationToken);

            var newMappings = new MappedIdentity[missingKeys.Count];
            for (int i = 0; i < missingKeys.Count; i++)
            {
                newMappings[i] = new MappedIdentity(missingKeys[i], identities[i]);
            }

            try
            {
                // Attempt bulk insert (unordered to continue on duplicates)
                if (newMappings.Length > 0)
                {
                    await _collection.InsertManyAsync(newMappings, new InsertManyOptions { IsOrdered = false }, cancellationToken);

                    // Add newly created mappings to result
                    foreach (var mapping in newMappings)
                    {
                        returnMapping[mapping.ExternalKey] = mapping.AggregateId;
                    }
                }
            }
            catch (MongoBulkWriteException ex)
            {
                // Some keys might have been inserted concurrently by another process
                // Re-fetch the missing keys to get the final mappings
                var stillMissingKeys = missingKeys
                    .Where(k => !returnMapping.ContainsKey(k))
                    .ToList();

                if (stillMissingKeys.Count > 0)
                {
                    var refetchedMappings = _collection
                        .Find(Builders<MappedIdentity>.Filter.In("ExternalKey", stillMissingKeys))
                        .ToEnumerable()
                        .ToDictionary(_ => _.ExternalKey, _ => _.AggregateId, StringComparer.InvariantCultureIgnoreCase);

                    foreach (var kvp in refetchedMappings)
                    {
                        returnMapping[kvp.Key] = kvp.Value;
                    }
                }

                // Log the concurrent insertion for diagnostics
                Logger.DebugFormat("Concurrent insertions detected during batch translation. {0} duplicates handled.",
                    ex.WriteErrors.Count);
            }

            return returnMapping;
        }

        /// <summary>
        /// Try to translate multiple external keys to identities without creating missing mappings.
        /// Uses a single database call.
        /// </summary>
        /// <param name="externalKeys">The external keys to translate.</param>
        /// <param name="cancellationToken"></param>
        /// <returns>Dictionary mapping external keys to identities. Missing keys are not included.</returns>
        protected Task<IReadOnlyDictionary<String, TKey>> TryTranslateBatchAsync(IEnumerable<String> externalKeys, CancellationToken cancellationToken = default)
        {
            return TranslateBatchAsync(externalKeys, createOnMissing: false, cancellationToken: cancellationToken);
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

            return mapped?.AggregateId;
        }

        protected MappedIdentity MapIdentity(string externalKey, TKey key)
        {
            try
            {
                //we need to be sure that the externalkey is lowercase.
                var lowercasedKey = externalKey.ToLowerInvariant();
                var mapped = new MappedIdentity(lowercasedKey, key);
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
