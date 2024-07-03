using Castle.Core.Logging;
using Jarvis.Framework.Shared.ReadModel.Atomic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Jarvis.Framework.Kernel.ProjectionEngine.Atomic;

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

    /// <summary>
    /// Logger
    /// </summary>
    public ILogger Logger { get; set; } = NullLogger.Instance;

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
    /// Allow to subscribe to the event when a readmodel is fixed
    /// </summary>
    public event EventHandler<AtomicReadmodelFixedEventArgs> ReadmodelFixed;

    private void OnReadmodelFixed(Type readmodelType)
    {
        ReadmodelFixed?.Invoke(this, new AtomicReadmodelFixedEventArgs { ReadModelType = readmodelType });
    }

    /// <summary>
    /// Raised for each single readmodel fixed, this is useful to react to a single
    /// readmodel changed during fixing.
    /// </summary>
    public event EventHandler<SingleAtomicReadmodelFixedEventArgs> SingleAtomicReadmodelFixed;

    private void OnSingleReadmodelFixed(string identity, Type readmodelType)
    {
        SingleAtomicReadmodelFixed?.Invoke(this, new SingleAtomicReadmodelFixedEventArgs { Identity = identity, ReadModelType = readmodelType });
    }

    /// <summary>
    /// We do not want to depend on anything else, we simply want to fix a bunch of readmodels
    /// without <see cref="AtomicProjectionEngine"/> or other dependencies
    /// </summary>
    /// <param name="readmodelToFix"></param>
    public void AddReadmodelToFix(Type readmodelToFix)
    {
        var executorType = typeof(ActionExecutor<>).MakeGenericType(readmodelToFix);
        IFixExecutor executorInstance = (IFixExecutor)Activator.CreateInstance(executorType,
            new Object[]
            {
                _atomicReadModelFactory,
                _atomicMongoCollectionWrapperFactory,
                _liveAtomicReadModelProcessor,
                Logger
            });

        executorInstance.ReadmodelFixed += (s, e) => OnReadmodelFixed(e.ReadModelType);
        executorInstance.SingleAtomicReadmodelFixed += (s, e) => OnSingleReadmodelFixed(e.Identity, e.ReadModelType);

        _executors.Add(executorInstance);
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

    public bool AllFixerFinishedFixing() => _executors.All(_ => _.FinishedFixing);

    /// <summary>
    /// Needed to work with Activator CreateInstance to cast to a simple interface that
    /// allows for basic functionality.
    /// </summary>
    private interface IFixExecutor
    {
        void StartFixing();

        void StopFixing();

        event EventHandler<AtomicReadmodelFixedEventArgs> ReadmodelFixed;
        event EventHandler<SingleAtomicReadmodelFixedEventArgs> SingleAtomicReadmodelFixed;

        bool FinishedFixing { get; }
    }

    private class ActionExecutor<T> : IFixExecutor
        where T : IAtomicReadModel
    {
        private readonly IAtomicCollectionWrapper<T> _collection;
        private readonly ILiveAtomicReadModelProcessor _liveAtomicReadModelProcessor;
        private readonly IAtomicReadModelFactory _atomicReadModelFactory;
        private readonly ILogger _logger;

        public event EventHandler<AtomicReadmodelFixedEventArgs> ReadmodelFixed;

        private void OnReadmodelFixed(Type readmodelType)
        {
            ReadmodelFixed?.Invoke(this, new AtomicReadmodelFixedEventArgs { ReadModelType = readmodelType });
        }

        public event EventHandler<SingleAtomicReadmodelFixedEventArgs> SingleAtomicReadmodelFixed;

        private void OnSingleReadmodelFixed(string identity, Type readmodelType)
        {
            SingleAtomicReadmodelFixed?.Invoke(this, new SingleAtomicReadmodelFixedEventArgs { Identity = identity, ReadModelType = readmodelType });
        }

        public bool FinishedFixing { get; private set; }

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
            bool fixedAtLeastOne = false;
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
                        //Signal that the readmodel is finished fixing and we at least fixed one readmodel
                        if (fixedAtLeastOne)
                        {
                            OnReadmodelFixed(typeof(T));
                        }
                        FinishedFixing = true;
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

                        //To avoid problem with exception we will create a readmodel without any event then catchup
                        T fixedRm = _atomicReadModelFactory.Create<T>(elementToFix.Id);
                        try
                        {
                            await _liveAtomicReadModelProcessor.CatchupAsync(fixedRm).ConfigureAwait(false);
                            fixedAtLeastOne = true;
                            OnSingleReadmodelFixed(elementToFix.Id, typeof(T));
                        }
                        catch (Exception ex)
                        {
                            _logger.ErrorFormat(ex, "Error during polling for fixer of readmodel {0}/{1} Last Position {2} - {3}", typeof(T), elementToFix.Id, fixedRm?.ProjectedPosition, ex.Message);
                            fixedRm.MarkAsFaulted(fixedRm.ProjectedPosition);
                        }
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

/// <summary>
/// Signal that a readmodel type is fixed, it means that AT LEAST one readmodel
/// was fixed, and we fixed everything for that type.
/// </summary>
public class AtomicReadmodelFixedEventArgs : EventArgs
{
    /// <summary>
    /// Type of the readmodel that was fixed.
    /// </summary>
    public Type ReadModelType { get; set; }
}

/// <summary>
/// Signal to the caller that we have a single readmodel that was fixed.
/// </summary>
public class SingleAtomicReadmodelFixedEventArgs : EventArgs
{
    /// <summary>
    /// Identity of single readmodel that get fixed
    /// </summary>
    public string Identity { get; set; }

    /// <summary>
    /// Type of the readmodel that get fixed.
    /// </summary>
    public Type ReadModelType { get; set; }
}
