using Castle.Core.Logging;
using Jarvis.Framework.Shared.Helpers;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
        Boolean Associate(String key , TId id);

        /// <summary>
        /// Associate the id to the key, but if an association was already present
        /// for id, it will update. It fails if key is already associated to another
        /// id.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="id"></param>
        /// <returns></returns>
        Boolean AssociateOrUpdate(String key, TId id);
    }

    /// <summary>
    /// Allow association of a string with an Id, it is similar to 
    /// <see cref="AbstractIdentityTranslator{TKey}"/> but this class does not
    /// allow remapping and it is used to setup a permanent and unique association between 
    /// a string and an Id. No another Id could be associated to the very same string except
    /// if the association is broken
    /// </summary>
    public abstract class MongoStringToIdentityAssociator<TId>  : IStringToIdentityAssociator<TId>
        where TId : EventStoreIdentity
    {
        public ILogger Logger { get; set; }

        private readonly IMongoCollection<Association> _collection;

        protected IMongoCollection<Association> Collection { get { return _collection; } }

        protected class Association
        {
            [BsonId]
            public string Key { get; private set; }

            [BsonElement]
            public TId Id { get; private set; }

            public Association(string key, TId id)
            {
                Key = key;
                Id = id;
            }
        }

        private readonly Boolean _allowDuplicateId;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="systemDB"></param>
        /// <param name="collectionName"></param>
        /// <param name="allowDuplicateId">If true allow two different key to be mapped to the same Id, if false
        /// ensure that an id can be associated only to a single key and vice versa</param>
        protected MongoStringToIdentityAssociator(IMongoDatabase systemDB, String collectionName, Boolean allowDuplicateId = true)
        {
            _collection = systemDB.GetCollection<Association>(collectionName);
            _allowDuplicateId = allowDuplicateId;
            if (allowDuplicateId == false)
            {
                _collection.Indexes.CreateOne(Builders<Association>.IndexKeys.Ascending(a => a.Id), new CreateIndexOptions() { Unique = true });
            }
            Logger = NullLogger.Instance;
        }

        public virtual Boolean Associate(String key, TId id)
        {
            return OnAssociate(key, id, a => _collection.InsertOne(a));
        }


        public bool AssociateOrUpdate(string key, TId id)
        {
            return OnAssociate(key, id, a => {
                DeleteAssociationWithId(id);
                OnAssociate(key, id, a1 => _collection.InsertOne(a1));
            });
        }

        protected virtual Boolean OnAssociate(String key, TId id, Action<Association> saveRoutine)
        {
            try
            {
                var mapped = new Association(key, id);
                saveRoutine(mapped);
                if (Logger.IsDebugEnabled) Logger.DebugFormat("Mapping between {0} and {1} was created correctly!", key, id);
                return true;
            }
            catch (Exception ex)
            {
                if (ex is MongoWriteException || ex is MongoWriteConcernException)
                {
                    if (ex.Message.Contains("E11000 duplicate key"))
                    {
                        //grab record by key or id, one of them conflicted
                        var existing = _collection.FindOneById(key) ??
                            _collection.Find(Builders<Association>.Filter.Eq(a => a.Id, id)).Single();
                        if (existing.Id == id && existing.Key == key)
                        {
                            if (Logger.IsDebugEnabled) Logger.DebugFormat("Mapping between {0} and {1} is already existing and is allowed!", key, id);
                            return true;
                        }
                        if (Logger.IsDebugEnabled) Logger.DebugFormat("Mapping between {0} and {1} is rejected because of duplication!", key, id);
                        return false;
                    }
                }
                throw;
            }
        }



        public void DeleteAssociationWithId(TId id)
        {
            if (Logger.IsDebugEnabled) Logger.DebugFormat("Deleted association of id {0}!", id);
            _collection.DeleteMany(Builders<Association>.Filter.Eq(a => a.Id, id));
        }

        public void DeleteAssociationWithKey(String key)
        {
            if (Logger.IsDebugEnabled) Logger.DebugFormat("Deleted association of key {0}!", key);
            _collection.DeleteMany(Builders<Association>.Filter.Eq(a => a.Key, key));
        }

        public String GetKeyFromId(TId id)
        {
            var association = _collection.Find(Builders<Association>.Filter.Eq(a => a.Id, id)).SingleOrDefault();
            if (association == null) return null;

            return association.Key;
        }

        public TId GetKeyFromKey(String key)
        {
            var association = _collection.FindOneById(key);
            if (association == null) return null;

            return association.Id;
        }

      
    }
}
