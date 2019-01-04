using Castle.Core.Logging;
using Jarvis.Framework.Shared.ReadModel.Atomic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Jarvis.Framework.Kernel.ProjectionEngine.Atomic
{
    /// <summary>
    /// Simple class that has the duty to fix all readmodel when the 
    /// signature is changed.
    /// </summary>
    public class AtomicReadModelSignatureFixer
    {
        private readonly IAtomicCollectionWrapperFactory _atomicMongoCollectionWrapperFactory;
        private readonly ILiveAtomicReadModelProcessor _liveAtomicReadModelProcessor;
        private readonly List<IFixExecutor> _executors;
        private readonly IAtomicReadModelFactory _atomicReadModelFactory;

        public ILogger Logger { get; set; }

        public AtomicReadModelSignatureFixer(
            IAtomicReadModelFactory atomicReadModelFactory,
            IAtomicCollectionWrapperFactory atomicMongoCollectionWrapperFactory,
            ILiveAtomicReadModelProcessor liveAtomicReadModelProcessor)
        {
            _atomicMongoCollectionWrapperFactory = atomicMongoCollectionWrapperFactory;
            _liveAtomicReadModelProcessor = liveAtomicReadModelProcessor ?? throw new ArgumentNullException(nameof(liveAtomicReadModelProcessor));
            _atomicReadModelFactory = atomicReadModelFactory;
            _executors = new List<IFixExecutor>();
            Logger = NullLogger.Instance;
        }

        /// <summary>
        /// We do not want to depend on anything else, we simply want to fix a bunch of readmodels
        /// without <see cref="AtomicProjectionEngine"/> or other dependencies
        /// </summary>
        /// <param name="readmodelToFix"></param>
        public void AddReadmodelToFix(Type readmodelToFix)
        {
            var executor = typeof(ActionExecutor<>).MakeGenericType(readmodelToFix);
            _executors.Add((IFixExecutor)Activator.CreateInstance(executor,
                new Object[]
                {
                    _atomicReadModelFactory,
                    _atomicMongoCollectionWrapperFactory,
                    _liveAtomicReadModelProcessor,
                    Logger
                })
           );
        }

        public void StartFixing()
        {
            //we need to start fixing all readmodels, we like to have a dedicated thread for each readmodel.
            foreach (var fixExecutor in _executors)
            {
                fixExecutor.StartFixing();
            }
        }

        public void StopFixing()
        {
            foreach (var fixExecutor in _executors)
            {
                fixExecutor.StopFixing();
            }
        }

        private interface IFixExecutor
        {
            void StartFixing();

            void StopFixing();
        }

        private class ActionExecutor<T> : IFixExecutor
            where T : IAtomicReadModel
        {
            private readonly IAtomicCollectionWrapper<T> _collection;
            private readonly ILiveAtomicReadModelProcessor _liveAtomicReadModelProcessor;
            private readonly IAtomicReadModelFactory _atomicReadModelFactory;
            private readonly ILogger _logger;

#pragma warning disable S1144 // Unused private types or members should be removed
            public ActionExecutor(
                IAtomicReadModelFactory atomicReadModelFactory,
                IAtomicCollectionWrapperFactory atomicCollectionWrapperFactory,
                ILiveAtomicReadModelProcessor liveAtomicReadModelProcessor,
                ILogger logger)
            {
                if (atomicCollectionWrapperFactory == null)
                {
                    throw new ArgumentNullException(nameof(atomicCollectionWrapperFactory));
                }

                _collection = atomicCollectionWrapperFactory.CreateCollectionWrappper<T>();
                _liveAtomicReadModelProcessor = liveAtomicReadModelProcessor ?? throw new ArgumentNullException(nameof(liveAtomicReadModelProcessor));
                _logger = logger;
                _atomicReadModelFactory = atomicReadModelFactory;
            }
#pragma warning restore S1144 // Unused private types or members should be removed

            public void StartFixing()
            {
                if (_started)
                    return;

                _started = true;
                Task.Factory.StartNew(InnerFix);
            }

            private Boolean _started;

            public void StopFixing()
            {
                _started = false;
            }

            private async Task InnerFix()
            {
                Int64 fixCheckpoint = 0;
                var actualVersion = _atomicReadModelFactory.GetReamdodelVersion(typeof(T));
                Int32 count = 0;
                //cycle until exit point.
                while (true)
                {
                    try
                    {
                        //Sorting for version we will start fixing readmodel never re-fixing 
                        var blockList = _collection
                            .AsQueryable()
                            .Where(_ => _.ReadModelVersion < actualVersion
                                && _.ProjectedPosition > fixCheckpoint)
                            .OrderBy(_ => _.ProjectedPosition)
                            .Take(100)
                            .Select(_ => new
                            {
                                _.Id,
                                _.ProjectedPosition
                            })
                            .ToList();
                        if (blockList.Count == 0)
                        {
                            _logger.InfoFormat("Finished fixing {0} - Fixed {1} readmodels", typeof(T), count);
                            return;
                        }
                        foreach (var elementToFix in blockList)
                        {
                            if (!_started)
                            {
                                return;
                            }

                            fixCheckpoint = elementToFix.ProjectedPosition;
                            count++;
                            _logger.DebugFormat("Fixing readmodel {0}", elementToFix.Id);
                            var fixedRm = await _liveAtomicReadModelProcessor.ProcessAsync<T>(elementToFix.Id, Int32.MaxValue).ConfigureAwait(false);
                            await _collection.UpdateAsync(fixedRm).ConfigureAwait(false);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.ErrorFormat(ex, "Error during polling for fixer of readmodel {0} - {1}", typeof(T), ex.Message);
                    }
                }
            }
        }
    }
}
