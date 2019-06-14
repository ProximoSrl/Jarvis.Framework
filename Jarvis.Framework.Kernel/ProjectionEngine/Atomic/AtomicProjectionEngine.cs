using Castle.Core.Logging;
using Jarvis.Framework.Kernel.ProjectionEngine.Atomic.Support;
using Jarvis.Framework.Kernel.ProjectionEngine.Client;
using Jarvis.Framework.Kernel.Support;
using Jarvis.Framework.Shared;
using Jarvis.Framework.Shared.Exceptions;
using Jarvis.Framework.Shared.Helpers;
using Jarvis.Framework.Shared.ReadModel.Atomic;
using Jarvis.Framework.Shared.Store;
using NStore.Core.Logging;
using NStore.Core.Persistence;
using NStore.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using System.Timers;

namespace Jarvis.Framework.Kernel.ProjectionEngine.Atomic
{
    /// <summary>
    /// This is a real reduced form of Projection Engine that is 
    /// capable of building only <see cref="Jarvis.Framework.Shared.ReadModel.Atomic.AbstractAtomicReadModel{TKey}IAtomicReadModel/>.
    /// 
    /// This component automatically flushes with an internal timer the checkpoint.
    /// </summary>
    public class AtomicProjectionEngine
    {
        private readonly AtomicProjectionCheckpointManager _atomicProjectionCheckpointManager;
        private readonly IPersistence _persistence;
        private readonly IAtomicReadmodelProjectorHelperFactory _atomicReadmodelProjectorHelperFactory;

        /// <summary>
        /// Poller to grab commits.
        /// </summary>
        private PollingClient _mainPoller;

        /// <summary>
        /// This poller is used to avoid that a new readmodel or some readmodel that
        /// is far behing will stops the engine.
        /// </summary>
        private PollingClient _catchupPoller;

        /// <summary>
        /// Each poller should start polling from the minimum dispatcher position of all readmodel
        /// that will be handled by that specific poller.
        /// </summary>
        private readonly Dictionary<Int32, Int64> _pollerStartingPoint = new Dictionary<int, long>();

        private readonly ICommitEnhancer _commitEnhancer;

        public ILogger Logger { get; set; }

        private readonly INStoreLoggerFactory _nStoreLoggerFactory;

        /// <summary>
        /// When the projection engine works, it will instruct the tracker to flush not for
        /// each commit projected, because all readmodels are idempotent. It is better to flush 
        /// periodically.
        /// </summary>
        public TimeSpan FlushTimeSpan { get; set; }

        private Timer _flushTimer;

        public IAtomicReadModelInitializer[] AllReadModelSetup { get; set; }

        /// <summary>
        /// By default we have a null notifier, but the user can register in castle
        /// a specific notifier to do whathever it want.
        /// </summary>
        public IAtomicReadmodelNotifier AtomicReadmodelNotifier { get; set; } = new NullAtomicReadmodelNotifier();

        /// <summary>
        /// This is populated only at startup, it is the higher position that was dispatched by the atomic projection 
        /// engine and it is used to understand if some readmodel should be projected using catch-up strategy.
        /// </summary>
        private readonly Int64 _lastPositionDispatched;

        /// <summary>
        /// Used for health checks, it stores last exception for each dispatcher, to understand if a dispatcher
        /// was halted.
        /// </summary>
        private readonly Dictionary<String, Exception> _engineExceptions = new Dictionary<String, Exception>();

        public AtomicProjectionEngine(
            IPersistence persistence,
            ICommitEnhancer commitEnhancer,
            AtomicProjectionCheckpointManager atomicProjectionCheckpointManager,
            IAtomicReadmodelProjectorHelperFactory atomicReadmodelProjectorHelperFactory,
            INStoreLoggerFactory nStoreLoggerFactory)
        {
            Logger = NullLogger.Instance;
            _atomicProjectionCheckpointManager = atomicProjectionCheckpointManager;
            _persistence = persistence;
            _commitEnhancer = commitEnhancer;
            _atomicReadmodelProjectorHelperFactory = atomicReadmodelProjectorHelperFactory;
            _lastPositionDispatched = _atomicProjectionCheckpointManager.GetLastPositionDispatched();
            _nStoreLoggerFactory = nStoreLoggerFactory;
            FlushTimeSpan = TimeSpan.FromSeconds(30); //flush checkpoint on db each 10 seconds.

            Logger.Info("Created Atomic Projection Engine");
            MaximumDifferenceForCatchupPoller = 20000;

            Metrics.HealthChecks.RegisterHealthCheck("AtomicProjectionEngine", (Func<Metrics.HealthCheckResult>) GetHealthCheck);
        }

        private Metrics.HealthCheckResult GetHealthCheck()
        {
            if (_engineExceptions.Count == 0)
                return Metrics.HealthCheckResult.Healthy();

            StringBuilder errors = new StringBuilder();
            foreach (var ex in _engineExceptions)
            {
                errors.AppendLine($"Error: {ex.Key}: {ex.Value}");
            }
            return Metrics.HealthCheckResult.Unhealthy("Error in Atomic Projection Engine: " + errors.ToString());
        }

        /// <summary>
        /// This is the limit value when a readmodel will be polled with catchup poller.
        /// When a dispatched position for a readmodel is behind maximum dispatched by more
        /// of this value, it will be projected with catchup poller.
        /// </summary>
        public Int64 MaximumDifferenceForCatchupPoller { get; set; }

        private Boolean _started = false;

        private const Int32 CatchupPollerId = 1;
        private const Int32 DefaultPollerId = 0;

        /*
          *those two are useful for catchup poller 
        */
        private readonly HashSet<String> _catchupPollerIdList = new HashSet<string>();
        private Dictionary<Type, List<AtomicDispatchChunkConsumer>> _catchupConsumerBlocks;

        private Int64 _defaultPollerStartingPoint;

        public async Task StartAsync()
        {
            if (_started)
            {
                return;
            }

            //Now it is time to Mark missing readmodels.
            await _atomicProjectionCheckpointManager.MarkMissingReadmodels().ConfigureAwait(false);

            Logger.Info("Started atomic projection engine");
            if (AllReadModelSetup != null)
            {
                foreach (var readmodelSetup in AllReadModelSetup)
                {
                    try
                    {
                        await readmodelSetup.Initialize().ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        Logger.ErrorFormat(ex, "Error with initializer {0}", readmodelSetup.GetType().FullName);
                        _engineExceptions[readmodelSetup.GetType().FullName] = ex;
                    }
                }
            }

            //we need to create a poller to dispatch everything to each readmodel.
            CreateTplChain(ref _tplBuffer, ref _tplBroadcaster);

            _defaultPollerStartingPoint = CreatePollerAndStart(ref _mainPoller, DefaultPollerId, DispatchToTpl);

            //Need to know if we ned to start catchup poller
            var behindReadmodels = _consumerBlocks
                .Values
                .SelectMany(_ => _.Where(i => i.PollerId == CatchupPollerId))
                .ToList();

            if (behindReadmodels.Count > 0)
            {
                _catchupPollerIdList.Clear();
                //recreate a dictionary of type and list of all readmodel that we need to catchup.
                _catchupConsumerBlocks = _consumerBlocks
                    .Where(b => b.Value.Any(c => c.PollerId == CatchupPollerId))
                    .ToDictionary(b => b.Key, b => b.Value.Where(p => p.PollerId == CatchupPollerId).ToList());

                Logger.InfoFormat("Catchup Poller started because some readmodel are too far behind: {0}", String.Join(", ", behindReadmodels.Select(_ => _.Consumer.AtomicReadmodelInfoAttribute.Name)));
                if (JarvisFrameworkGlobalConfiguration.AtomicProjectionEngineOptimizedCatchup)
                {
                    CreatePollerAndStart(ref _catchupPoller, CatchupPollerId, DispatchCatchupToTpl);
                }
                else
                {
                    CreatePollerAndStart(ref _catchupPoller, CatchupPollerId, DispatchToTpl);
                }
            }

            _flushTimer = new Timer(FlushTimeSpan.TotalMilliseconds);
            _flushTimer.Elapsed += FlushTimerElapsed;
            _flushTimer.Start();

            _started = true;
        }

        /// <summary>
        /// Create a poller, start the poller and return starting position of the poller.
        /// </summary>
        /// <param name="poller"></param>
        /// <param name="pollerId"></param>
        /// <returns></returns>
        private Int64 CreatePollerAndStart(ref PollingClient poller, Int32 pollerId, Func<AtomicDispatchChunk, Task<Boolean>> dispatchToTplFunction)
        {
            if (poller != null)
            {
                poller.Stop();
            }
            var startingPosition = TryGetStartingPoint(pollerId);
            Logger.InfoFormat("AtomicProjectionEngine: Starting poller id {0} from position {1}", pollerId, startingPosition);
            var subscription = new JarvisFrameworkLambdaSubscription(c => dispatchToTplFunction(new AtomicDispatchChunk(pollerId, c)));
            poller = new PollingClient(_persistence, startingPosition, subscription, _nStoreLoggerFactory);
            poller.Start();
            return startingPosition;
        }

        /// <summary>
        /// Catchup poller is different, instead of sending everything to real TPL, it will simply
        /// fix readmodels until we reach minimum dispatch, then finally it will simply dispatch 
        /// to the standard tpl.
        /// </summary>
        /// <param name="atomicDispatchChunk"></param>
        /// <returns></returns>
        private async Task<Boolean> DispatchCatchupToTpl(AtomicDispatchChunk atomicDispatchChunk)
        {
            //Process only if we have a payload
            if (atomicDispatchChunk?.Chunk?.Payload != null)
            {
                //if catchup poller reach starting point of the engine, it can start sending data to TPL
                if (atomicDispatchChunk.Chunk.Position > _defaultPollerStartingPoint)
                {
                    //Catchup poller reached the original checkpoint, we can start doing standard projection.
                    return await DispatchToTpl(atomicDispatchChunk).ConfigureAwait(false);
                }

                //to be more efficient, until we reach the startup commit of default, we can project
                //differently, because we can simply project all the id in one shot.
                //we need to completely reconstruct all readnodel that should be catched.
                if (atomicDispatchChunk.Chunk.Payload is Changeset cs)
                {
                    var identity = cs.GetIdentity();
                    if (identity != null) //remember empty commits.
                    {
                        if (_catchupConsumerBlocks.TryGetValue(identity.GetType(), out var consumers))
                        {
                            //if I already projected this id, nothing to do.
                            if (!_catchupPollerIdList.Contains(identity.AsString()))
                            {
                                foreach (var consumer in consumers)
                                {
                                    await consumer.Consumer.FullProject(identity).ConfigureAwait(false);
                                    _atomicProjectionCheckpointManager.MarkPosition(
                                        consumer.Consumer.AtomicReadmodelInfoAttribute.Name,
                                        atomicDispatchChunk.Chunk.Position);
                                }

                                _catchupPollerIdList.Add(identity.AsString());
                            }
                        }
                    }
                }

                //Clear everything when we reach the ponit
                if (atomicDispatchChunk.Chunk.Position == _defaultPollerStartingPoint)
                {
                    _catchupPollerIdList.Clear(); //we need to clear all the in memory id list.

                    foreach (var consumer in _catchupConsumerBlocks.Values.SelectMany(c => c))
                    {
                        _atomicProjectionCheckpointManager.MarkPosition(
                            consumer.Consumer.AtomicReadmodelInfoAttribute.Name,
                            atomicDispatchChunk.Chunk.Position);
                    }

                }
            }

            return true;
        }

        private async Task<Boolean> DispatchToTpl(AtomicDispatchChunk atomicDispatchChunk)
        {
            var result = await _tplBuffer.SendAsync(atomicDispatchChunk).ConfigureAwait(false);
            if (!result)
            {
                Logger.ErrorFormat("Unable to dispatch chunk {0} for poller {1}", atomicDispatchChunk.Chunk.Position, atomicDispatchChunk.PollerId);
                throw new JarvisFrameworkEngineException(String.Format("Unable to dispatch chunk {0} for poller {1}", atomicDispatchChunk.Chunk.Position, atomicDispatchChunk.PollerId));
            }
            return result;
        }

        private Boolean _flusing = false;

        private void FlushTimerElapsed(object sender, ElapsedEventArgs e)
        {
            if (!_flusing)
            {
                try
                {
                    _flusing = true;
                    _atomicProjectionCheckpointManager.FlushAsync().Wait();
                }
                finally
                {
                    _flusing = false;
                }
            }
        }

        /// <summary>
        /// Stop polling and returns a task that can be waited if you want to be sure
        /// that TPL has flushed.
        /// </summary>
        /// <returns></returns>
        public async Task StopAsync()
        {
            if (_started)
            {
                Logger.Info("Stopped atomic projection engine");
                _mainPoller?.Stop();
                _flushTimer?.Stop();
                await _atomicProjectionCheckpointManager.FlushAsync().ConfigureAwait(false);
                if (_tplBuffer != null)
                {
                    _tplBuffer.Complete(); //Complete the tplChain.
                    _started = false;
                    await _tplBroadcaster.Completion;
                }
            }
        }

        #region TPL

        /*
            We have two TPL chain, the first one is used for standard dispatch, the second one is
            used for all atomic readmodels that starts from 0 (new readmodel) we do not want to wait
            for the new readmodel to catch up, so we need another poller.ù
       
            To keep it simple we have two tpl, one for the standard polling the other for the catchup.
        */
        private BufferBlock<AtomicDispatchChunk> _tplBuffer;
        private ITargetBlock<AtomicDispatchChunk> _tplBroadcaster;

        /// <summary>
        /// For each Type of the ID (aggregate) we have a list of consumer (multiple atomic
        /// reamodel for an aggregate type)
        /// </summary>
        private readonly Dictionary<Type, List<AtomicDispatchChunkConsumer>> _consumerBlocks
            = new Dictionary<Type, List<AtomicDispatchChunkConsumer>>();

        private void CreateTplChain(ref BufferBlock<AtomicDispatchChunk> buffer, ref ITargetBlock<AtomicDispatchChunk> broadcaster)
        {
            DataflowBlockOptions bufferOptions = new DataflowBlockOptions
            {
                BoundedCapacity = 4000,
            };
            var localBuffer = buffer = new BufferBlock<AtomicDispatchChunk>(bufferOptions);
            Metrics.Metric.Gauge("atomic-projection-buffer", () => localBuffer.Count, Metrics.Unit.Items);

            ExecutionDataflowBlockOptions executionOption = new ExecutionDataflowBlockOptions
            {
                BoundedCapacity = 4000,
            };
            var enhancer = new TransformBlock<AtomicDispatchChunk, AtomicDispatchChunk>(c =>
            {
                _commitEnhancer.Enhance(c.Chunk); //This was already done by the poller, but we are investigating on using directly a NStore poller.
                return c;
            }, executionOption);
            buffer.LinkTo(enhancer, new DataflowLinkOptions() { PropagateCompletion = true });
            Metrics.Metric.Gauge("atomic-projection-enhancer-buffer", () => (Double)enhancer.InputCount, Metrics.Unit.Items);

            var consumers = new List<ITargetBlock<AtomicDispatchChunk>>();
            foreach (var item in _consumerBlocks)
            {
                //Ok I have a list of atomic projection, we need to create projector for every item.
                var blocks = item.Value;
                var consumer = new ActionBlock<AtomicDispatchChunk>(InnerDispatch(item, blocks), executionOption);
                Metrics.Metric.Gauge("atomic-projection-consumer-buffer-" + item.Key.Name, () => consumer.InputCount, Metrics.Unit.Items);
                consumers.Add(consumer);
            }
            broadcaster = GuaranteedDeliveryBroadcastBlock.Create(consumers, "AtomicPoller", 3000);
            enhancer.LinkTo(broadcaster, new DataflowLinkOptions() { PropagateCompletion = true });
        }

        private Func<AtomicDispatchChunk, Task> InnerDispatch(
            KeyValuePair<Type, List<AtomicDispatchChunkConsumer>> item,
            List<AtomicDispatchChunkConsumer> blocks)
        {
            return async dispatchObj =>
            {
                try
                {
                    if (dispatchObj.Chunk.Payload is Changeset changeset)
                    {
                        var aggregateId = changeset.GetIdentity();
                        if (aggregateId?.GetType() == item.Key)
                        {
                            //Remember to dispatch this only to objects that are associated to this poller. Remember that we build a single
                            //TPL chain, so both the poller (default and catchup) will push data inside the very same tpl pipeline, but inside
                            //this dispatcher chain, we simply dispatch only to the blocks that have correct poller id.
                            foreach (var atomicDispatchChunkConsumer in blocks.Where(_ => _.PollerId == dispatchObj.PollerId))
                            {
                                if (Logger.IsDebugEnabled)
                                {
                                    Logger.DebugFormat("Dispatched chunk {0} with poller {1} for readmodel {2}", dispatchObj.Chunk.Position, dispatchObj.PollerId, atomicDispatchChunkConsumer.Consumer.AtomicReadmodelInfoAttribute.Name);
                                }

                                //let the abstract readmodel handle everyting, then finally dispatch notification.Handle method internally
                                //catches exception and make atomic readmodel faulty.
                                var handleReturnValue = await atomicDispatchChunkConsumer.Consumer.Handle(
                                    dispatchObj.Chunk.Position,
                                    changeset,
                                    aggregateId).ConfigureAwait(false);

                                if (handleReturnValue != null)
                                {
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                                    //I do not care if the notification works or not, I simply call it and forget.
                                    if (handleReturnValue.CreatedForFirstTime)
                                    {
                                        AtomicReadmodelNotifier.ReadmodelCreatedAsync(handleReturnValue.Readmodel, changeset);
                                    }
                                    else
                                    {
                                        AtomicReadmodelNotifier.ReadmodelUpdatedAsync(handleReturnValue.Readmodel, changeset);
                                    }
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                                }
                            }
                        }
                    }
                    foreach (var rm in blocks.Where(_ => _.PollerId == dispatchObj.PollerId))
                    {
                        _atomicProjectionCheckpointManager.MarkPosition(rm.Consumer.AtomicReadmodelInfoAttribute.Name, dispatchObj.Chunk.Position);
                    }
                }
                catch (Exception ex)
                {
                    //TODO: Implement health check with failed
                    _engineExceptions[item.Key.Name] = ex;
                    Logger.ErrorFormat(ex, "Generic error in TPL dispatching {0} with poller {1} for id type {2}", dispatchObj.Chunk.Position, dispatchObj.PollerId, item.Key);
                    throw;
                }
            };
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Scan an entire assembly trying to find all atomic readmodels that
        /// are present in the assembly. Each of this readmodel will be projected
        /// separately from the others.
        /// </summary>
        /// <param name="assembly"></param>
        /// <returns></returns>
        public AtomicProjectionEngine RegisterAllAtomicReadmodelsFromAssembly(Assembly assembly)
        {
            foreach (var type in assembly.GetTypes()
                .Where(_ => !_.IsAbstract && typeof(AbstractAtomicReadModel).IsAssignableFrom(_)))
            {
                RegisterAtomicReadModel(type);
            }
            return this;
        }

        /// <summary>
        /// Register an atomic readmodel and return false if the readmodel is too old to 
        /// be projected, it should be projected by another catchup projection engine.
        /// </summary>
        /// <param name="atomicReadmodelType"></param>
        /// <returns></returns>
        public AtomicProjectionEngine RegisterAtomicReadModel(Type atomicReadmodelType)
        {
            var attribute = AtomicReadmodelInfoAttribute.GetFrom(atomicReadmodelType);
            var projectionPosition = _atomicProjectionCheckpointManager.GetCheckpoint(attribute.Name);
            Logger.InfoFormat("Registered atomic readmodel {0} starting from position  {1}", attribute.Name, projectionPosition);

            if (_lastPositionDispatched - projectionPosition > MaximumDifferenceForCatchupPoller)
            {
                Logger.InfoFormat("Readmodel {0} was registered in Atomic Projection Engine with catchup poller because it is too old. Last dispatched is {1} but we already dispatched {2}",
                     attribute.Name,
                     projectionPosition,
                     _lastPositionDispatched);

                AddConsumer(projectionPosition, CatchupPollerId, atomicReadmodelType, attribute);
                return this;
            }

            //default poller
            Logger.InfoFormat("Readmodel {0} registered in default poller in Atomic Projection Engine", attribute.Name);
            AddConsumer(projectionPosition, DefaultPollerId, atomicReadmodelType, attribute);
            return this;
        }

        private void AddConsumer(Int64 projectedPosition, Int32 pollerId, Type atomicReadmodelType, AtomicReadmodelInfoAttribute atomicReadmodelInfoAttribute)
        {
            //The real starting point is the minimum between actual position and what is already in the dictionary, each poller id should start from 
            //minimum dispatched to avoid missing data.
            _pollerStartingPoint[pollerId] = Math.Min(TryGetStartingPoint(pollerId), projectedPosition);

            if (!_consumerBlocks.TryGetValue(atomicReadmodelInfoAttribute.AggregateIdType, out List<AtomicDispatchChunkConsumer> atomicReadmodelEventConsumers))
            {
                atomicReadmodelEventConsumers = new List<AtomicDispatchChunkConsumer>();
                _consumerBlocks.Add(atomicReadmodelInfoAttribute.AggregateIdType, atomicReadmodelEventConsumers);
            }
            //add the consumer
            if (!atomicReadmodelEventConsumers.Any(_ => _.Consumer.AtomicReadmodelInfoAttribute.Name == atomicReadmodelInfoAttribute.Name))
            {
                var consumer = _atomicReadmodelProjectorHelperFactory.CreateFor(atomicReadmodelType);
                var atomicConsumer = new AtomicDispatchChunkConsumer(consumer, pollerId);
                atomicReadmodelEventConsumers.Add(atomicConsumer);
            }

            _atomicProjectionCheckpointManager.Register(atomicReadmodelType);
        }

        /// <summary>
        /// We want to take for each poller id the minimum value.
        /// </summary>
        /// <param name="pollerId"></param>
        /// <returns></returns>
        private Int64 TryGetStartingPoint(int pollerId)
        {
            if (_pollerStartingPoint.TryGetValue(pollerId, out var value))
            {
                return value;
            }

            //Start from Maximum, result will be passed to min function.
            return Int64.MaxValue;
        }

        #endregion

        #region Internal Classes

        internal class AtomicDispatchChunk
        {
            public AtomicDispatchChunk(int pollerId, IChunk chunk)
            {
                PollerId = pollerId;
                Chunk = chunk;
            }

            public Int32 PollerId { get; private set; }

            public IChunk Chunk { get; private set; }
        }

        internal class AtomicDispatchChunkConsumer
        {
            public AtomicDispatchChunkConsumer(IAtomicReadmodelProjectorHelper consumer, int pollerId)
            {
                Consumer = consumer;
                PollerId = pollerId;
            }

            public IAtomicReadmodelProjectorHelper Consumer { get; private set; }

            public Int32 PollerId { get; private set; }
        }

        internal class BehindAtomicReadModel
        {
            public AtomicReadmodelInfoAttribute Attribute { get; set; }

            public Type ReadModelType { get; set; }
        }

        #endregion
    }
}