using Castle.Core.Logging;
using Jarvis.Framework.Shared.Exceptions;
using Jarvis.Framework.Shared.Helpers;
using Jarvis.Framework.Shared.ReadModel.Atomic;
using MongoDB.Driver;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;

namespace Jarvis.Framework.Kernel.ProjectionEngine.Atomic
{
    /// <summary>
    /// Simple manager to store in mongodb the position of the last
    /// dispatched checkpoint for each atomic readmodel.
    /// </summary>
    public class AtomicProjectionCheckpointManager
    {
        private readonly IMongoCollection<AtomicProjectionCheckpoint> _collection;
        private readonly ConcurrentDictionary<String, AtomicProjectionCheckpoint> _inMemoryCheckpoint;
        private readonly Dictionary<String, Type> _registeredTypeNames = new Dictionary<String, Type>();

        /// <summary>
        /// The logger
        /// </summary>
        public ILogger Logger { get; set; }

        /// <summary>
        /// Asyncrounsly tell when a checkpoint changed, so if you are interested in this event
        /// you can be notified when a specific checkpoint changed.
        /// </summary>
        public event EventHandler<AtomicProjectionCheckpointChangedEventArgs> CheckpointChanged;

        /// <summary>
        /// Create the manager and load from db all existing checkpoints.
        /// </summary>
        /// <param name="readmodelDb"></param>
        public AtomicProjectionCheckpointManager(
            IMongoDatabase readmodelDb)
        {
            _collection = readmodelDb.GetCollection<AtomicProjectionCheckpoint>("framework.AtomicProjectionCheckpoint");
#pragma warning disable S2971 // "IEnumerable" LINQs should be simplified
            var dictionary = _collection
                .AsQueryable()
                .Where(c => c.ReadmodelMissing == false)
                .ToList()
                .Select(_ => new KeyValuePair<String, AtomicProjectionCheckpoint>(_.Id, _));
#pragma warning restore S2971 // "IEnumerable" LINQs should be simplified

            _inMemoryCheckpoint = new ConcurrentDictionary<String, AtomicProjectionCheckpoint>(dictionary);
            Logger = NullLogger.Instance;
        }

        /// <summary>
        /// Raises event
        /// </summary>
        /// <param name="e"></param>
        protected virtual void OnCheckpointChanged(AtomicProjectionCheckpointChangedEventArgs e)
        {
            // fire and forget.
            Task.Run(() => CheckpointChanged?.Invoke(this, e));
        }

        public IEnumerable<Type> GetAllRegisteredAtomicReadModels()
        {
            return _registeredTypeNames.Values;
        }

        public void Register(Type atomicAggregateType)
        {
            var attribute = AtomicReadmodelInfoAttribute.GetFrom(atomicAggregateType);

            if (attribute == null)
            {
                throw new JarvisFrameworkEngineException($"Type {atomicAggregateType} cannot be used as atomic readmodel, it misses the AtomicReadmodelInfo attribute");
            }

            if (_registeredTypeNames.ContainsKey(attribute.Name))
            {
                if (_registeredTypeNames[attribute.Name] == atomicAggregateType)
                    return; //already added.

                throw new JarvisFrameworkEngineException($"Type {atomicAggregateType} cannot be used as atomic readmodel, it uses name {attribute.Name} already in use by type {_registeredTypeNames[attribute.Name]}");
            }

            //Simply store all information in memory, we simply need to initialize latest checkpoint if not reloaded
            if (!_inMemoryCheckpoint.ContainsKey(attribute.Name))
            {
                AtomicProjectionCheckpoint atomicProjectionCheckpoint = new AtomicProjectionCheckpoint()
                {
                    Id = attribute.Name,
                    Position = 0
                };
                _inMemoryCheckpoint.AddOrUpdate(attribute.Name, atomicProjectionCheckpoint, (k, e) => atomicProjectionCheckpoint);
            }
            _registeredTypeNames.Add(attribute.Name, atomicAggregateType);
        }

        /// <summary>
        /// After the caller called <see cref="Register(Type)"/> for all the readmodels, calling this
        /// method will mark all other readmodel as missing.
        /// </summary>
        /// <returns></returns>
        public async Task MarkMissingReadmodels()
        {
            HashSet<String> allregisteredTypes = new HashSet<string>();
            foreach (var key in _registeredTypeNames.Keys)
            {
                allregisteredTypes.Add(key);
            }
            var missings = _inMemoryCheckpoint.Where(c => !allregisteredTypes.Contains(c.Key));
            foreach (var missing in missings)
            {
                var checkpoint = missing.Value;
                checkpoint.ReadmodelMissing = true;
                await _collection.SaveAsync(checkpoint, checkpoint.Id).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Retrieve checkpoint for a single readmodel.
        /// </summary>
        /// <param name="atomicReadmodelName"></param>
        /// <returns></returns>
        public long GetCheckpoint(string atomicReadmodelName)
        {
            if (_inMemoryCheckpoint.TryGetValue(atomicReadmodelName, out var checkpoint))
            {
                return checkpoint.Position;
            }
            return 0;
        }

        /// <summary>
        /// Mark last dispatched checkpoint for a given readmodel.
        /// </summary>
        /// <param name="name">The name of the Atomic Readmodel, as taken from attribute.</param>
        /// <param name="value">Last checkpoint dispatched.</param>
        public void MarkPosition(String name, Int64 value)
        {
            if (_inMemoryCheckpoint.TryGetValue(name, out var checkpoint))
            {
                if (value > checkpoint.Position)
                {
                    if (Logger.IsDebugEnabled) Logger.DebugFormat("Mark atomic readmodel {0} to checkpoint {1}", name, value);
                    checkpoint.Position = value;
                }
                else
                {
                    if (Logger.IsDebugEnabled) Logger.DebugFormat("try to mark atomic readmodel {0} to dispatched position {1} but checkpoint is greater {2}", name, value, checkpoint.Position);
                }
            }
            else
            {
                Logger.ErrorFormat("Unable to mark position to readmodel {0}, readmodel not known", name);
            }
            OnCheckpointChanged(new AtomicProjectionCheckpointChangedEventArgs(name));
        }

        /// <summary>
        /// Simply return the minimum checkpoint already dispatched.
        /// </summary>
        /// <returns></returns>
        public long GetMinimumPositionDispatched()
        {
            if (_inMemoryCheckpoint.Count == 0)
                return 0;

            return _inMemoryCheckpoint.Values.Where(m => !m.ReadmodelMissing).Min(_ => _.Position);
        }

        /// <summary>
        /// Simply return the Maximum checkpoint already dispatched.
        /// </summary>
        /// <returns></returns>
        public long GetLastPositionDispatched()
        {
            if (_inMemoryCheckpoint.Count == 0)
                return 0;

            return _inMemoryCheckpoint.Values.Max(_ => _.Position);
        }

        public async Task FlushAsync()
        {
            var requests = new List<WriteModel<AtomicProjectionCheckpoint>>();

            // Add the replace operations to the requests list
            foreach (var inMemoryCheckpoint in _inMemoryCheckpoint.Values)
            {
                var replaceOneModel = new ReplaceOneModel<AtomicProjectionCheckpoint>(
                    Builders<AtomicProjectionCheckpoint>.Filter.Eq("_id", inMemoryCheckpoint.Id),
                    inMemoryCheckpoint)
                {
                    IsUpsert = true
                };
                requests.Add(replaceOneModel);
            }

            await _collection.BulkWriteAsync(requests,
                new BulkWriteOptions()
                {
                    IsOrdered = false,
                });
        }

        private class AtomicProjectionCheckpoint
        {
            /// <summary>
            /// This is the name of the readmodel taken from the
            /// <see cref="AtomicReadmodelInfoAttribute"/>
            /// </summary>
            public String Id { get; set; }

            /// <summary>
            /// This is the last checkpoint dispatched.
            /// </summary>
            public Int64 Position { get; set; }

            /// <summary>
            /// This property is true when the readmodel was not anymore present 
            /// in projection engine. It is useful because if a readmodel was removed
            /// <see cref="Position"/> will fall behind and this will immediately
            /// show that the readmodel is really missing.
            /// </summary>
            public Boolean ReadmodelMissing { get; set; }
        }
    }

    /// <summary>
    /// A checkpoint changed for a specific atomic readmodel.
    /// </summary>
    public class AtomicProjectionCheckpointChangedEventArgs : EventArgs
    {
        public AtomicProjectionCheckpointChangedEventArgs(string atomicReadmodelName)
        {
            AtomicReadmodelName = atomicReadmodelName;
        }

        public string AtomicReadmodelName { get; private set; }
    }
}
