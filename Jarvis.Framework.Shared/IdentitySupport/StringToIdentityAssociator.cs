using Castle.Core.Logging;
using Jarvis.Framework.Shared.Helpers;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using System;
using System.Globalization;
using System.Linq;

namespace Jarvis.Framework.Shared.IdentitySupport
{
    public interface IStringToIdentityAssociator<TId> where TId : EventStoreIdentity
    {
        /// <summary>
        /// Create an association between key and id, it fails if key was already 
        /// associated. It could fail if id was already associated to multiple key
        /// but this depends by the specific implementation.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="id"></param>
        /// <returns></returns>
        Boolean Associate(String key, TId id);

        /// <summary>
        /// Associate the id to the key, but if an association was already present
        /// for id, it will update. It fails if key is already associated to another
        /// id.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="id"></param>
        /// <returns></returns>
        Boolean AssociateOrUpdate(String key, TId id);

        /// <summary>
        /// Delete an association between a key and an id.
        /// </summary>
        /// <param name="key"></param>
        /// <returns>The number of association that were deleted.</returns>
        Int32 DeleteAssociationWithKey(String key);

        /// <summary>
        /// Delete an association of a key with a specific id
        /// </summary>
        /// <param name="id"></param>
        /// <returns>The number of association that were deleted.</returns>
        Int32 DeleteAssociationWithId(TId id);

        /// <summary>
        /// Given a key returns the id that is associated to that key, it returns
        /// null if the key has not association.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        TId GetIdFromKey(String key);
    }

    /// <summary>
    /// Allow association of a string with an Id, it is similar to 
    /// <see cref="AbstractIdentityTranslator{TKey}"/> but this class does not
    /// allow remapping and it is used to setup a permanent and unique association between 
    /// a string and an Id. No another Id could be associated to the very same string except
    /// if the association is broken
    /// </summary>
    /// <typeparam name="TId"></typeparam>
    public abstract class MongoStringToIdentityAssociator<TId> : IStringToIdentityAssociator<TId>
        where TId : EventStoreIdentity
    {
        protected readonly ILogger _logger;

        private readonly IMongoCollection<Association> _collection;

        protected IMongoCollection<Association> Collection { get { return _collection; } }

        protected class Association
        {
            /// <summary>
            /// This object is slightly strange, the Key of the mapping is the id used
            /// on mongo collection so will be translated with the _id value. 
            /// </summary>
            [BsonId]
            public string Key { get; set; }

            /// <summary>
            /// This is the original key, useful for case insensitiveness.
            /// </summary>
            [BsonElement]
            public String OriginalKey { get; set; }

            [BsonElement]
            public TId Id { get; set; }

            public Association(string key, String originalKey, TId id)
            {
                Key = key;
                OriginalKey = originalKey;
                Id = id;
            }
        }

        private readonly Boolean _caseInsensitive;

        /// <summary>
        /// Construct the associator
        /// </summary>
        /// <param name="systemDB"></param>
        /// <param name="collectionName"></param>
        /// <param name="logger"></param>
        /// <param name="allowDuplicateId">If true allow two different key to be mapped to the same Id, if false
        /// ensure that an id can be associated only to a single key and vice versa. </param>
        /// <param name="caseInsensitive">True if we want this association to be case insensitive.</param>
        protected MongoStringToIdentityAssociator(
            IMongoDatabase systemDB,
            String collectionName,
            ILogger logger,
            Boolean allowDuplicateId = true,
            Boolean caseInsensitive = true)
        {
            _collection = systemDB.GetCollection<Association>(collectionName);
            _logger = logger;

            if (!allowDuplicateId)
            {
                _collection.Indexes.CreateOne(
                    new CreateIndexModel<Association>(
                        Builders<Association>.IndexKeys.Ascending(a => a.Id),
                        new CreateIndexOptions() { Unique = true })
                );
            }
            _collection.Indexes.CreateOne(
                new CreateIndexModel<Association>(
                    Builders<Association>.IndexKeys.Ascending(a => a.OriginalKey),
                    new CreateIndexOptions() { Name = "OriginalKey" })
                );
            _caseInsensitive = caseInsensitive;

            //old version did not support case sensitiveness, and was case sensitive. we need to normalize the data.
            NormalizeExistingData();
        }

        /// <summary>
        /// First version of this component did not store the original key, we need to move from the esisting version
        /// to the new version
        /// </summary>
        private void NormalizeExistingData()
        {
            var allElements = _collection.AsQueryable()
                .Where(e => e.OriginalKey == null)
                .OrderBy(e => e.Id);
            foreach (var element in allElements)
            {
                try
                {
                    element.OriginalKey = element.Key;
                    //If we are case insensitive, and key is not all lowercase, we need to change the key.
                    if (_caseInsensitive && element.Key != element.Key.ToLower(CultureInfo.InvariantCulture))
                    {
                        //this is where we can have problem, because we need to check if we have a conflicting
                        //registration, two key that differs only with casing.
                        var conflicting = _collection.AsQueryable()
#pragma warning disable RCS1155 // Use StringComparison when comparing strings.
#pragma warning disable S1449 //Sonar; Use locale whenb use toLower, ignored because this is a regex query in LINQ
                            .Any(e => e.Key.ToLower() == element.Key.ToLower() && e.Key != element.Key);
#pragma warning restore RCS1155 // Use StringComparison when comparing strings.
#pragma warning restore S1449
                        if (!conflicting)
                        {
                            var originalKey = element.Key;
                            element.Key = element.Key.ToLower(CultureInfo.InvariantCulture);
                            _collection.RemoveById(originalKey);
                            try
                            {
                                _collection.InsertOne(element);
                            }
                            catch (Exception ex)
                            {
                                _logger.Error($"Unable to reinsert the lowercase identity {element.Key} - we reinsert the original one.", ex);
                                element.Key = originalKey;
                                _collection.InsertOne(element);
                            }

                            continue;
                        }
                        _logger.Error($"Unable to normalize StringToIdentityAssociation, collection {_collection.CollectionNamespace.CollectionName} on database {_collection.CollectionNamespace.DatabaseNamespace.DatabaseName}: we have duplication with casing with key {element.Key}, but the mapping should be case insensitive");
                    }

                    //Simply save the new version, key is not changed.
                    _collection.Save(element, element.Key);
                }
                catch (Exception ex)
                {
                    //Probably we already have case sensitiveness duplication, 
                    _logger.Warn($"Unable to normalize StringToIdentityAssociation, collection {_collection.CollectionNamespace.CollectionName} on database {_collection.CollectionNamespace.DatabaseNamespace.DatabaseName} with key {element.Key}: {ex.Message}", ex);
                }
            }
        }

        public virtual Boolean Associate(String key, TId id)
        {
            return OnAssociate(key, id, a => _collection.InsertOne(a));
        }

        public bool AssociateOrUpdate(string key, TId id)
        {
            var associatedId = GetIdFromKey(key);
            if (associatedId != null && associatedId != id)
                return false; //cannot associate the key to more than one id.

            Boolean result = true;
            OnAssociate(key, id, a =>
            {
                DeleteAssociationWithId(id);
                var innerResult = OnAssociate(key, id, a1 => _collection.InsertOne(a1));
                if (!innerResult)
                {
                    result = false;
                }
            });
            return result;
        }

        protected virtual Boolean OnAssociate(String key, TId id, Action<Association> saveRoutine)
        {
            if (String.IsNullOrEmpty(key))
                throw new ArgumentNullException(nameof(key));

            if (id == null)
                throw new ArgumentNullException(nameof(id));

            string idKey = GetIdKeyFromKey(key);
            try
            {
                var mapped = new Association(idKey, key, id);
                saveRoutine(mapped);
                if (_logger.IsDebugEnabled) _logger.DebugFormat("Mapping between {0} and {1} was created correctly!", key, id);
                return true;
            }
            catch (Exception ex)
            {
                bool isMongoException = ex is MongoWriteException || ex is MongoWriteConcernException;
                if (isMongoException && ex.Message.Contains("E11000 duplicate key"))
                {
                    //grab record by key or id, or the one that conflicted, remember that with concurrency
                    //it could be possible that another thread removed the key.
                    var existing = _collection.FindOneById(idKey) ??
                        _collection.Find(Builders<Association>.Filter.Eq(a => a.Id, id)).SingleOrDefault();
                    if (existing == null)
                    {
                        //The key conflicted, but I'm unable to find conflict record in db, another thread removed it? I cannot associate
                        if (_logger.IsWarnEnabled) _logger.WarnFormat("Mapping between {0} and {1} was probably removed by a different thread removed by a different thread, abort!", key, id);
                        return false;
                    }
                    if (existing.Id == id && existing.Key == idKey)
                    {
                        if (_logger.IsDebugEnabled) _logger.DebugFormat("Mapping between {0} and {1} is already existing and is allowed!", key, id);
                        return true;
                    }
                    _logger.ErrorFormat("Mapping between {0} and {1} is rejected because of duplication! Mapper is {2}", key, id, typeof(TId));
                    return false;
                }
                throw;
            }
        }

        /// <summary>
        /// Based on the fact that the collection is or not case insensitive, 
        /// transform the key to lowercase, to have a mongodb index that
        /// is not using casing.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        private string GetIdKeyFromKey(string key)
        {
            var idKey = key;
            if (_caseInsensitive)
            {
                idKey = key.ToLower(CultureInfo.InvariantCulture);
            }

            return idKey;
        }

        public Int32 DeleteAssociationWithId(TId id)
        {
            if (id == null)
                throw new ArgumentNullException(nameof(id));

            if (_logger.IsDebugEnabled) _logger.DebugFormat("Deleted association of id {0}!", id);
            var deleteResult = _collection.DeleteMany(Builders<Association>.Filter.Eq(a => a.Id, id));
            return (Int32)deleteResult.DeletedCount;
        }

        /// <summary>
        /// Return the number of association deleted.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
		public Int32 DeleteAssociationWithKey(String key)
        {
            if (String.IsNullOrEmpty(key))
                throw new ArgumentNullException(nameof(key));

            var idKey = GetIdKeyFromKey(key);
            if (_logger.IsDebugEnabled) _logger.DebugFormat("Deleted association of key {0}!", key);
            var deleteResult = _collection.DeleteMany(Builders<Association>.Filter.Eq(a => a.Key, idKey));
            return (Int32)deleteResult.DeletedCount;
        }

        public String GetKeyFromId(TId id)
        {
            if (id == null)
                throw new ArgumentNullException(nameof(id));

            var association = _collection.Find(Builders<Association>.Filter.Eq(a => a.Id, id)).SingleOrDefault();
            if (association == null) return null;

            return association.OriginalKey ?? association.Key;
        }

        public TId GetIdFromKey(String key)
        {
            if (String.IsNullOrEmpty(key))
                throw new ArgumentNullException(nameof(key));

            var idKey = GetIdKeyFromKey(key);
            var association = _collection.FindOneById(idKey);
            if (association == null) return null;

            return association.Id;
        }
    }
}
