using Castle.Core.Logging;
using Jarvis.Framework.Kernel.Events;
using Jarvis.Framework.Kernel.MultitenantSupport;
using Jarvis.Framework.Kernel.ProjectionEngine.Client;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using Jarvis.Framework.Kernel.Support;
using System.Collections.Concurrent;
using Metrics;
using Jarvis.Framework.Shared.Exceptions;
using Jarvis.Framework.Shared.Logging;
using Jarvis.Framework.Shared.Messages;
using Jarvis.Framework.Shared.Helpers;

namespace Jarvis.Framework.Kernel.ProjectionEngine
{
    public sealed class ProjectionEngine : ITriggerProjectionsUpdate
    {
        private readonly ICommitPollingClientFactory _pollingClientFactory;

        private ICommitPollingClient[] _clients;
        private readonly Dictionary<BucketInfo, ICommitPollingClient> _bucketToClient;

        private readonly IConcurrentCheckpointTracker _checkpointTracker;

        private readonly IHousekeeper _housekeeper;
        private readonly INotifyCommitHandled _notifyCommitHandled;

        private long _maxDispatchedCheckpoint = 0;
        private Int32 _countOfConcurrentDispatchingCommit = 0;

        private readonly ILogger _logger;

        private readonly ILoggerThreadContextManager _loggerThreadContextManager;

        public ILoggerFactory LoggerFactory { get; set; }

        private readonly IPersistence _persistence;
        private readonly ProjectionEngineConfig _config;
        private readonly Dictionary<string, IProjection[]> _projectionsBySlot;
        private readonly IProjection[] _allProjections;

        /// <summary>
        /// this is a list of all FATAL error occurred in projection engine. A fatal
        /// error means that some slot is stopped and so there should be an health
        /// check that fails.
        /// </summary>
        private readonly ConcurrentBag<String> _engineFatalErrors;

        public Boolean Initialized { get; private set; }

        private System.Timers.Timer _flushTimer;

        /// <summary>
        /// This is the basic constructor for the projection engine.
        /// </summary>
        /// <param name="pollingClientFactory"></param>
        /// <param name="persistence"></param>
        /// <param name="checkpointTracker"></param>
        /// <param name="projections"></param>
        /// <param name="housekeeper"></param>
        /// <param name="notifyCommitHandled"></param>
        /// <param name="config"></param>
        /// <param name="logger"></param>
        /// <param name="loggerThreadContextManager"></param>
        public ProjectionEngine(
            ICommitPollingClientFactory pollingClientFactory,
            IPersistence persistence,
            IConcurrentCheckpointTracker checkpointTracker,
            IProjection[] projections,
            IHousekeeper housekeeper,
            INotifyCommitHandled notifyCommitHandled,
            ProjectionEngineConfig config,
            ILogger logger,
            ILoggerThreadContextManager loggerThreadContextManager)
        {
            if (pollingClientFactory == null)
                throw new ArgumentNullException(nameof(pollingClientFactory));

            if (checkpointTracker == null)
                throw new ArgumentNullException(nameof(checkpointTracker));

            if (projections == null)
                throw new ArgumentNullException(nameof(projections));

            if (housekeeper == null)
                throw new ArgumentNullException(nameof(housekeeper));

            if (notifyCommitHandled == null)
                throw new ArgumentNullException(nameof(notifyCommitHandled));

            if (config == null)
                throw new ArgumentNullException(nameof(config));

            if (persistence == null)
                throw new ArgumentNullException(nameof(persistence));
            if (projections == null)
                throw new ArgumentNullException(nameof(projections));
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            _persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _loggerThreadContextManager = loggerThreadContextManager ?? throw new ArgumentNullException(nameof(loggerThreadContextManager));
            _pollingClientFactory = pollingClientFactory ?? throw new ArgumentNullException(nameof(pollingClientFactory));
            _checkpointTracker = checkpointTracker ?? throw new ArgumentNullException(nameof(checkpointTracker));
            _housekeeper = housekeeper ?? throw new ArgumentNullException(nameof(housekeeper));
            _notifyCommitHandled = notifyCommitHandled ?? throw new ArgumentNullException(nameof(notifyCommitHandled));

            var configErrors = config.Validate();
            if (!String.IsNullOrEmpty(configErrors))
            {
                throw new ArgumentException(configErrors, nameof(config));
            }

            _engineFatalErrors = new ConcurrentBag<string>();

            _pollingClientFactory = pollingClientFactory;
            _checkpointTracker = checkpointTracker;
            _housekeeper = housekeeper;
            _notifyCommitHandled = notifyCommitHandled;
            _config = config;

            if (_config.Slots[0] != "*")
            {
                projections = projections
                    .Where(x => _config.Slots.Any(y => y == x.Info.SlotName))
                    .ToArray();
            }

            if (OfflineMode.Enabled)
            {
                //In offline mode we already have filtered out the projection.
                _allProjections = projections;
            }
            else
            {
                //we are not in offline mode, just use all projection that are not marked to 
                //run only in offline mode
                logger.Info($"Projection engine is NOT in OFFLINE mode, projection engine will run only projection that are not marked for OfflineModel.");
                _allProjections = projections.Where(_ => !_.Info.OfflineProjection).ToArray();
            }

            _projectionsBySlot = projections
                .GroupBy(x => x.Info.SlotName)
                .ToDictionary(x => x.Key, x => x.OrderByDescending(p => p.Priority).ToArray());

            _bucketToClient = new Dictionary<BucketInfo, ICommitPollingClient>();

            _flushTimer = new System.Timers.Timer
            {
                Interval = 10000
            };
            _flushTimer.Elapsed += FlushTimerElapsed;

            RegisterHealthCheck();
        }

        private void DumpProjections()
        {
            if (_logger.IsDebugEnabled)
            {
                foreach (var prj in _allProjections)
                {
                    if (_logger.IsDebugEnabled) _logger.DebugFormat("Projection: {0}", prj.GetType().FullName);
                }

                if (_allProjections.Length == 0)
                {
                    _logger.Warn("no projections found!");
                }
            }
        }

        /// <summary>
        /// This is only to give the ability to register and start with old startable aux function
        /// for castle.
        /// </summary>
        public void StartSync()
        {
            StartAsync().Wait();
        }

        public async Task StartAsync()
        {
            if (_logger.IsDebugEnabled) _logger.DebugFormat("Starting projection engine on tenant {0}", _config.TenantId);

            await StartPollingAsync().ConfigureAwait(false);
            if (_logger.IsDebugEnabled) _logger.Debug("Projection engine started");
        }

        public async Task PollAsync()
        {
            if (!Initialized)
                return; //Poller still not initialized.

            foreach (var client in _clients)
            {
                await client.PollAsync().ConfigureAwait(false);
            }
        }

        public void Stop()
        {
            if (_logger.IsDebugEnabled) _logger.Debug("Stopping projection engine");
            StopPolling();
            if (_logger.IsDebugEnabled) _logger.Debug("Projection engine stopped");
        }

        public async Task StartWithManualPollAsync(Boolean immediatePoll = true)
        {
            if (_config.DelayedStartInMilliseconds == 0)
            {
                await InnerStartWithManualPollAsync(immediatePoll).ConfigureAwait(false);
            }
            else
            {
                await Task.Factory.StartNew(async () =>
                {
                    Thread.Sleep(_config.DelayedStartInMilliseconds);
                    await InnerStartWithManualPollAsync(immediatePoll).ConfigureAwait(false);
                }).ConfigureAwait(false);
            }
        }

        private async Task InnerStartWithManualPollAsync(Boolean immediatePoll)
        {
            await InitAsync().ConfigureAwait(false);
            if (immediatePoll)
            {
                await PollAsync().ConfigureAwait(false);
            }
        }

        private async Task StartPollingAsync()
        {
            await InitAsync().ConfigureAwait(false);
            foreach (var bucket in _bucketToClient)
            {
                bucket.Value.Start();
            }
            _flushTimer.Start();
        }

        private async Task InitAsync()
        {
            _maxDispatchedCheckpoint = 0;
            DumpProjections();

            TenantContext.Enter(_config.TenantId);

            await _housekeeper.InitAsync().ConfigureAwait(false);

            await ConfigureProjectionsAsync().ConfigureAwait(false);

            // cleanup
            await _housekeeper.RemoveAllAsync(_persistence).ConfigureAwait(false);

            var allSlots = _projectionsBySlot.Keys.ToArray();

            var allClients = new List<ICommitPollingClient>();

            //recreate all polling clients.
            foreach (var bucket in _config.BucketInfo)
            {
                string pollerId = "bucket: " + String.Join(",", bucket.Slots);
                var client = _pollingClientFactory.Create(_persistence, pollerId);
                allClients.Add(client);
                _bucketToClient.Add(bucket, client);
                client.Configure(GetStartGlobalCheckpoint(), 4000);
            }

            _clients = allClients.ToArray();

            foreach (var slotName in allSlots)
            {
                MetricsHelper.CreateMeterForDispatcherCountSlot(slotName);
                var startCheckpoint = GetStartCheckpointForSlot(slotName);
                _logger.InfoFormat("Slot {0} starts from {1}", slotName, startCheckpoint);

                var name = slotName;
                //find right consumer
                var slotBucket = _config.BucketInfo.SingleOrDefault(b =>
                    b.Slots.Any(s => s.Equals(slotName, StringComparison.OrdinalIgnoreCase))) ??
                    _config.BucketInfo.Single(b => b.Slots[0] == "*");
                var client = _bucketToClient[slotBucket];
                client.AddConsumer(commit => DispatchCommitAsync(commit, name, startCheckpoint));
            }

            Initialized = true;
            MetricsHelper.SetProjectionEngineCurrentDispatchCount(() => _countOfConcurrentDispatchingCommit);
        }

        private Int64 GetStartGlobalCheckpoint(String[] slots)
        {
            //if one of the slot is *, simply consider all active slots.
            if (slots.Any(_ => _ == "*"))
            {
                slots = _projectionsBySlot.Keys.ToArray();
            }
            var checkpoints = slots.Select(GetStartCheckpointForSlot).Distinct().ToArray();
            return checkpoints.Length > 0 ? checkpoints.Min() : 0;
        }

        private Int64 GetStartCheckpointForSlot(string slotName)
        {
            Int64 min = Int64.MaxValue;
            if (RebuildSettings.ShouldRebuild)
                throw new JarvisFrameworkEngineException("Projection engine is not used anymore for rebuild. Rebuild is done with the specific RebuildProjectionEngine");

            if (_projectionsBySlot.TryGetValue(slotName, out var projections))
            {
                foreach (var projection in projections)
                {
                    var currentValue = _checkpointTracker.GetCurrent(projection);
                    if (currentValue < min)
                    {
                        min = currentValue;
                    }
                }
            }
            return min;
        }

        private async Task ConfigureProjectionsAsync()
        {
            _checkpointTracker.SetUp(_allProjections, 1, true);
            foreach (var slot in _projectionsBySlot)
            {
                foreach (var projection in slot.Value)
                {
                    await projection.SetUpAsync().ConfigureAwait(false);
                }
            }
        }

        private void StopPolling()
        {
            if (_clients != null)
            {
                foreach (var client in _clients)
                {
                    client.Stop(false);
                }
                _clients = null;
            }
            _bucketToClient.Clear();
            _flushTimer.Stop();
        }

        private readonly ConcurrentDictionary<String, Int64> lastCheckpointDispatched =
            new ConcurrentDictionary<string, long>();

        private async Task DispatchCommitAsync(IChunk chunk, string slotName, Int64 startCheckpoint)
        {
            Interlocked.Increment(ref _countOfConcurrentDispatchingCommit);

            _loggerThreadContextManager.SetContextProperty("commit", $"{chunk.OperationId}/{chunk.Position}");

            if (_logger.IsDebugEnabled) _logger.DebugFormat("Dispatching checkpoit {0} on tenant {1}", chunk.Position, _config.TenantId);
            TenantContext.Enter(_config.TenantId);
            var chkpoint = chunk.Position;

            if (!lastCheckpointDispatched.ContainsKey(slotName))
            {
                lastCheckpointDispatched[slotName] = 0;
            }

            if (lastCheckpointDispatched[slotName] >= chkpoint)
            {
                var error = String.Format("Sequence broken, last checkpoint for slot {0} was {1} and now we dispatched {2}",
                        slotName, lastCheckpointDispatched[slotName], chkpoint);
                _logger.Error(error);
                throw new JarvisFrameworkEngineException(error);
            }

            if (lastCheckpointDispatched[slotName] + 1 != chkpoint && lastCheckpointDispatched[slotName] > 0)
            {
                _logger.DebugFormat("Sequence of commit not consecutive (probably holes), last dispatched {0} receiving {1}",
                  lastCheckpointDispatched[slotName], chkpoint);
            }
            lastCheckpointDispatched[slotName] = chkpoint;

            if (chkpoint <= startCheckpoint)
            {
                //Already dispatched, skip it.
                Interlocked.Decrement(ref _countOfConcurrentDispatchingCommit);
                return;
            }

            var projections = _projectionsBySlot[slotName];

            object[] events = GetArrayOfObjectFromChunk(chunk);

            var eventCount = events.Length;

            //now it is time to dispatch the commit, we will dispatch each projection 
            //and for each projection we dispatch all the events present in changeset
            foreach (var projection in projections)
            {
                var cname = projection.Info.CommonName;
                Object eventMessage = null;
                try
                {
                    //Foreach projection we need to dispach every event
                    for (int index = 0; index < eventCount; index++)
                    {
                        eventMessage = events[index];

                        var message = eventMessage as IMessage;
                        string eventName = eventMessage.GetType().Name;
                        _loggerThreadContextManager.SetContextProperty("evType", eventName);
                        _loggerThreadContextManager.SetContextProperty("evMsId", message?.MessageId);
                        _loggerThreadContextManager.SetContextProperty("evCheckpointToken", chunk.Position);
                        _loggerThreadContextManager.SetContextProperty("prj", cname);

                        var checkpointStatus = _checkpointTracker.GetCheckpointStatus(cname, chunk.Position);
                        long ticks = 0;

                        try
                        {
                            //pay attention, stopwatch consumes time.
                            var sw = new Stopwatch();
                            sw.Start();
                            await projection
                                .HandleAsync(eventMessage, checkpointStatus.IsRebuilding)
                                .ConfigureAwait(false);
                            sw.Stop();
                            ticks = sw.ElapsedTicks;
                            KernelMetricsHelper.IncrementProjectionCounter(cname, slotName, eventName, ticks, sw.ElapsedMilliseconds);

                            if (_logger.IsDebugEnabled)
                            {
                                _logger.DebugFormat("[Slot:{3}] [Projection {4}] Handled checkpoint {0}: {1} > {2}",
                                    chunk.Position,
                                    chunk.PartitionId,
                                    $"eventName: {eventName} [event N°{index}]",
                                    slotName,
                                    cname
                                );
                            }
                        }
                        catch (Exception ex)
                        {
                            var error = String.Format("[Slot: {3} Projection: {4}] Failed checkpoint: {0} StreamId: {1} Event Name: {2}",
                                chunk.Position,
                                chunk.PartitionId,
                                eventName,
                                slotName,
                                cname);
                            _logger.Fatal(error, ex);
                            _engineFatalErrors.Add(String.Format("{0}\n{1}", error, ex));
                            throw;
                        }

                    } //End of event cycle
                }
                catch (Exception ex)
                {
                    _loggerThreadContextManager.ClearContextProperty("commit");
                    _logger.ErrorFormat(ex, "Error dispathing Chunk [{0}]\n Message: {1}\n Error: {2}\n",
                            chunk.Position, eventMessage?.GetType()?.Name, ex.Message);
                    throw;
                }
                finally
                {
                    _loggerThreadContextManager.ClearContextProperty("evType");
                    _loggerThreadContextManager.ClearContextProperty("evMsId");
                    _loggerThreadContextManager.ClearContextProperty("evCheckpointToken");
                    _loggerThreadContextManager.ClearContextProperty("prj");
                }

                    projection.CheckpointProjected(chunk.Position);
                    flushNeeded = true;

#pragma warning disable S125 // Sections of code should not be "commented out"
                    //TODO: Evaluate if it is needed to update single projection checkpoint
                    //Update this projection, set all events of this checkpoint as dispatched.
                    //await _checkpointTracker.UpdateProjectionCheckpointAsync(cname, chunk.Position).ConfigureAwait(false);
#pragma warning restore S125 // Sections of code should not be "commented out"
                } //End of projection cycle.

            await _checkpointTracker.UpdateSlotAndSetCheckpointAsync(
                slotName,
                projections.Select(_ => _.Info.CommonName),
                chunk.Position).ConfigureAwait(false);
            KernelMetricsHelper.MarkCommitDispatchedCount(slotName, 1);

            await _notifyCommitHandled.SetDispatched(slotName, chunk).ConfigureAwait(false);

            // ok in multithread wihout locks!
            if (_maxDispatchedCheckpoint < chkpoint)
            {
                if (_logger.IsDebugEnabled)
                {
                    _logger.DebugFormat("Updating last dispatched checkpoint from {0} to {1}",
                        _maxDispatchedCheckpoint,
                        chkpoint
                    );
                }
                _maxDispatchedCheckpoint = chkpoint;
            }
            _loggerThreadContextManager.ClearContextProperty("commit");
            Interlocked.Decrement(ref _countOfConcurrentDispatchingCommit);
        }

        /// <summary>
        /// Simple optimization to avoid calling flush if the projection service
        /// is idle.
        /// </summary>
        private Boolean flushNeeded;
        private static object[] GetArrayOfObjectFromChunk(IChunk chunk)
        {
            Object[] events;

        /// <summary>
        /// Simply ask to the checkpoint collection tracker to flush everything.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FlushTimerElapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (flushNeeded)
            {
                flushNeeded = false;
                _checkpointTracker?.FlushCheckpointCollectionAsync().Wait();
            }
        }
            if (chunk.Payload is Changeset)
            {
                events = ((Changeset)chunk.Payload).Events;
            }
            else if (chunk.Payload != null)
            {
                events = new Object[] { chunk.Payload };
            }
            else
            {
                events = new Object[0];
            }

            return events;
        }

        public async Task UpdateAsync()
        {
            if (_logger.IsDebugEnabled) _logger.DebugFormat("Update triggered on tenant {0}", _config.TenantId);
            await PollAsync().ConfigureAwait(false);
        }

        public async Task UpdateAndWaitAsync()
        {
            if (!Initialized)
                throw new JarvisFrameworkEngineException("Projection engine was not inited, pleas call the InnerStartWithManualPollAsync method if you want to poll manually");

            if (_logger.IsDebugEnabled) _logger.DebugFormat("Polling from {0}", _config.EventStoreConnectionString);
            int retryCount = 0;
            long lastDispatchedCheckpoint = 0;

            while (true)
            {
                var checkpointId = await _persistence.ReadLastPositionAsync().ConfigureAwait(false);

                if (checkpointId > 0)
                {
                    if (_logger.IsDebugEnabled) _logger.DebugFormat("Last checkpoint is {0}", checkpointId);

                    await PollAsync().ConfigureAwait(false);
                    await Task.Delay(1000).ConfigureAwait(false);

                    var dispatched = _maxDispatchedCheckpoint;
                    if (_logger.IsDebugEnabled)
                    {
                        _logger.DebugFormat(
                            "Checkpoint {0} : Max dispatched {1}",
                            checkpointId, dispatched
                            );
                    }
                    if (checkpointId == dispatched)
                    {
                        checkpointId = await _persistence.ReadLastPositionAsync().ConfigureAwait(false);
                        if (checkpointId == dispatched)
                        {
                            if (_logger.IsDebugEnabled) _logger.Debug("Polling done!");
                            return;
                        }
                    }
                    else
                    {
                        if (dispatched > lastDispatchedCheckpoint)
                        {
                            lastDispatchedCheckpoint = dispatched;
                            retryCount = 0;
                        }
                        else
                        {
                            if (retryCount++ > 10)
                            {
                                throw new JarvisFrameworkEngineException("Polling failed, engine freezed at checkpoint " + dispatched);
                            }
                        }
                    }
                }
                else
                {
                    if (_logger.IsDebugEnabled) _logger.Debug("Event store is empty");
                    return;
                }
            }
        }

        #region Healt Checks

        private void RegisterHealthCheck()
        {
            HealthChecks.RegisterHealthCheck("Projection Engine Errors", () =>
            {
                if (_engineFatalErrors.Count == 0)
                    return HealthCheckResult.Healthy("No error in projection engine");

                return HealthCheckResult.Unhealthy("Error occurred in projection engine: " + String.Join("\n\n", _engineFatalErrors));
            });
        }

        #endregion
    }

    public class BucketInfo
    {
        public String[] Slots { get; set; }

        public Int32 BufferSize { get; set; }
    }
}
