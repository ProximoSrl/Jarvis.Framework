using System;
using System.Collections.Generic;
using System.Linq;
using Jarvis.Framework.Shared.ReadModel;

namespace Jarvis.Framework.Kernel.ProjectionEngine
{
    public interface IInmemoryCollection<TModel, in TKey> where TModel : IReadModelEx<TKey>
    {
        TModel GetById(TKey id);
        void Save(TModel model);
        IQueryable<TModel> GetAll();
        bool Delete(TKey id);
        void Clear();
        bool IsActive { get; }
        void Insert(TModel value);
        void Deactivate();
        void Activate();
    }

    [Serializable]
    public class DuplicatedElementException : Exception
    {
        public DuplicatedElementException(string id)
            :base(string.Format("Duplicated element with id {0}", id))
        {
        }

        public DuplicatedElementException() : base()
        {
        }

        public DuplicatedElementException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected DuplicatedElementException(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context) : base(info, context)
        {
        }
    }

    public class InmemoryCollection<TModel, TKey> : IInmemoryCollection<TModel, TKey> where TModel : IReadModelEx<TKey>
    {
        private readonly IDictionary<TKey, TModel> _cache = new Dictionary<TKey, TModel>();

        public TModel GetById(TKey id)
        {
            TModel model;
            if (_cache.TryGetValue(id, out model))
                return model;
            return default(TModel);
        }

        public void Save(TModel model) 
        {
            model.ThrowIfInvalidId();
            _cache[model.Id] = model;
        }

        public IQueryable<TModel> GetAll()
        {
            return _cache.Values.AsQueryable();
        }

        public bool Delete(TKey id)
        {
            return _cache.Remove(id);
        }

        public void Clear()
        {
            _cache.Clear();
        }

        public bool IsActive { get; private set; }

        public void Insert(TModel value)
        {
            if (_cache.ContainsKey(value.Id))
                throw new DuplicatedElementException(value.Id.ToString());

            _cache.Add(value.Id, value);
        }

        public void Deactivate()
        {
            IsActive = false;
            Clear();
        }

        public void Activate()
        {
            Clear();
            IsActive = true;
        }
    }

    public interface IRebuildContext
    {
        IInmemoryCollection<TModel, TKey> GetCollection<TModel, TKey>(string collectionName) where TModel : IReadModelEx<TKey>;
    }

    public class RebuildContext : IRebuildContext
    {
        private readonly bool _cacheEnabled;
        private readonly IDictionary<string, object> _context = new Dictionary<string, object>();

        public RebuildContext(bool cacheEnabled)
        {
            _cacheEnabled = cacheEnabled;
        }

        public IInmemoryCollection<TModel, TKey> GetCollection<TModel, TKey>(string collectionName) where TModel : IReadModelEx<TKey>
        {
            object untypedCollection;
            if (!_context.TryGetValue(collectionName, out untypedCollection))
            {
                var typedCollection = new InmemoryCollection<TModel, TKey>();
                _context[collectionName] = typedCollection;
                
                if (_cacheEnabled)
                    typedCollection.Activate();

                return typedCollection;
            }

            return (IInmemoryCollection<TModel, TKey>)untypedCollection;
        }
    }
}
