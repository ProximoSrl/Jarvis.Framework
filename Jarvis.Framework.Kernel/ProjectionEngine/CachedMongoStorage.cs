using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Jarvis.Framework.Shared.ReadModel;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Jarvis.Framework.Kernel.ProjectionEngine
{
    public class CachedMongoStorage<TModel, TKey> : IMongoStorage<TModel, TKey> where TModel : class, IReadModelEx<TKey>
    {
        readonly IInmemoryCollection<TModel, TKey> _inmemoryCollection;
        readonly MongoStorage<TModel, TKey> _storage;
        public CachedMongoStorage(
                IMongoCollection<TModel> collection,
                IInmemoryCollection<TModel, TKey> inmemoryCollection
            )
        {
            _storage = new MongoStorage<TModel, TKey>(collection);
            _inmemoryCollection = inmemoryCollection;
        }

        public bool IndexExists(IndexKeysDefinition<TModel> keys)
        {
            return _storage.IndexExists(keys);
        }

        void ThrowIfOperatingInMemory()
        {
            if (_inmemoryCollection.IsActive)
                throw new Exception("Unsupported operation while operating in memory");
        }

        public void CreateIndex(IndexKeysDefinition<TModel> keys, CreateIndexOptions options = null)
        {
            _storage.CreateIndex(keys, options);
        }

        public void InsertBatch(IEnumerable<TModel> values)
        {
            if (_inmemoryCollection.IsActive)
            {
                foreach (var value in values)
                {
                    _inmemoryCollection.Insert(value);
                }
            }
            else
            {
                _storage.InsertBatch(values);
            }
        }

        public IQueryable<TModel> All
        {
            get
            {
                return _inmemoryCollection.IsActive ? _inmemoryCollection.GetAll() : _storage.All;
            }
        }

        public TModel FindOneById(TKey id)
        {
            return _inmemoryCollection.IsActive ?
                _inmemoryCollection.GetById(id) :
                _storage.FindOneById(id);
        }

        public IQueryable<TModel> Where(Expression<Func<TModel, bool>> filter)
        {
            return _inmemoryCollection.IsActive ?
                _inmemoryCollection.GetAll().Where(filter).ToArray().AsQueryable() :
                _storage.Where(filter);
        }

        public bool Contains(Expression<Func<TModel, bool>> filter)
        {
            return _inmemoryCollection.IsActive ?
                _inmemoryCollection.GetAll().Any(filter) :
                _storage.Contains(filter);
        }

        public InsertResult Insert(TModel model)
        {
            if (_inmemoryCollection.IsActive)
            {
                try
                {
                    _inmemoryCollection.Insert(model);
                    return new InsertResult()
                    {
                        Ok = true
                    };
                }
                catch (Exception ex)
                {
                    return new InsertResult()
                    {
                        Ok = false,
                        ErrorMessage = ex.Message
                    };
                }
            }
            return _storage.Insert(model);
        }

        public SaveResult SaveWithVersion(TModel model, int orignalVersion)
        {
            if (_inmemoryCollection.IsActive)
            {
                // non posso controllare le versioni perché l'istanza è la stessa
                _inmemoryCollection.Save(model);
                return new SaveResult { Ok = true };
            }

            return _storage.SaveWithVersion(model, orignalVersion);
        }

        public DeleteResult Delete(TKey id)
        {
            if (_inmemoryCollection.IsActive)
            {
                return new DeleteResult
                {
                    Ok = true,
                    DocumentsAffected = _inmemoryCollection.Delete(id) ? 1 : 0
                };
            }

            return _storage.Delete(id);
        }

        public void Drop()
        {
            if (_inmemoryCollection.IsActive)
            {
                _inmemoryCollection.Clear();
            }
            _storage.Drop();
        }

        public IMongoCollection<TModel> Collection {
            get
            {
                ThrowIfOperatingInMemory();
                return _storage.Collection;
            }
        }


        public void Flush()
        {
            if (_inmemoryCollection.IsActive)
            {
                var allModels = _inmemoryCollection.GetAll().ToArray();
                if(allModels.Any())
                    _storage.InsertBatch(allModels);

                _inmemoryCollection.Deactivate();
            }
        }
    }
}