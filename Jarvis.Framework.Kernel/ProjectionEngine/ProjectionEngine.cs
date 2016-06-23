using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Castle.Core.Logging;
using Jarvis.Framework.Kernel.Events;
using Jarvis.Framework.Kernel.MultitenantSupport;
using Jarvis.Framework.Kernel.ProjectionEngine.Client;
using Jarvis.Framework.Shared.Events;
using Jarvis.Framework.Shared.Logging;
using Jarvis.Framework.Shared.MultitenantSupport;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using NEventStore;
using NEventStore.Client;
using NEventStore.Serialization;
using NEventStore.Persistence;
using Jarvis.Framework.Kernel.Support;
using System.Collections.Concurrent;
using System.Text;
using Jarvis.Framework.Shared.Helpers;

namespace Jarvis.Framework.Kernel.ProjectionEngine
{
    public class ProjectionEngine : ITriggerProjectionsUpdate
    {
        readonly Func<IPersistStreams, ICommitPollingClient> _pollingClientFactory;

        private List<ICommitPollingClient> _clients;
        private Dictionary<BucketInfo, ICommitPollingClient> _bucketToClient;

        readonly IConcurrentCheckpointTracker _checkpointTracker;

        readonly IHousekeeper _housekeeper;
        readonly INotifyCommitHandled _notifyCommitHandled;

        private readonly ProjectionMetrics _metrics;
        private long _maxDispatchedCheckpoint = 0;
        private Int32 _countOfConcurrentDispatchingCommit = 0;

        public IExtendedLogger Logger
        {
            get { return _logger; }
            set { _logger = value; }
        }
        IExtendedLogger _logger = NullLogger.Instance;

        public ILoggerFactory LoggerFactory
        {
            get { return _loggerFactory; }
            set { _loggerFactory = value; }
        }
        private ILoggerFactory _loggerFactory;

        IStoreEvents _eventstore;
        readonly ProjectionEngineConfig _config;
        readonly Dictionary<string, IProjection[]> _projectionsBySlot;
        private readonly IProjection[] _allProjections;

        private readonly IRebuildContext _rebuildContext;

        public ProjectionEngine(
            Func<IPersistStreams, ICommitPollingClient> pollingClientFactory,
            IConcurrentCheckpointTracker checkpointTracker,
            IProjection[] projections,
            IHousekeeper housekeeper,
            IRebuildContext rebuildContext,
            INotifyCommitHandled notifyCommitHandled,
            ProjectionEngineConfig config)
        {
            _pollingClientFactory = pollingClientFactory;
            _checkpointTracker = checkpointTracker;
            _housekeeper = housekeeper;
            _rebuildContext = rebuildContext;
            _notifyCommitHandled = notifyCommitHandled;
            _config = config;

            if (_config.Slots[0] != "*")
            {
                projections = projections
                    .Where(x => _config.Slots.Any(y => y == x.GetSlotName()))
                    .ToArray();
            }

            _allProjections = projections;
            _projectionsBySlot = projections
                .GroupBy(x => x.GetSlotName())
                .ToDictionary(x => x.Key, x => x.OrderByDescending(p => p.Priority).ToArray());

            _metrics = new ProjectionMetrics(_allProjections);
            _clients = new List<ICommitPollingClient>();
            _bucketToClient = new Dictionary<BucketInfo, ICommitPollingClient>();
        }

        private void DumpProjections()
        {
            if (Logger.IsDebugEnabled)
            {
                foreach (var prj in _allProjections)
                {
                    if (Logger.IsDebugEnabled) Logger.DebugFormat("Projection: {0}", prj.GetType().FullName);
                }

                if (_allProjections.Length == 0)
                {
                    Logger.Warn("no projections found!");
                }
            }
        }

        public void Start()
        {
            if (Logger.IsDebugEnabled) Logger.DebugFormat("Starting projection engine on tenant {0}", _config.TenantId);

            StartPolling();
            if (Logger.IsDebugEnabled) Logger.Debug("Projection engine started");
        }

        public void Poll()
        {
            foreach (var client in _clients)
            {
                client.Poll();
            }
        }

        public void Stop()
        {
            if (Logger.IsDebugEnabled) Logger.Debug("Stopping projection engine");
            StopPolling();
            if (Logger.IsDebugEnabled) Logger.Debug("Projection engine stopped");
        }

        public void StartWithManualPoll(Boolean immediatePoll = true)
        {
            if (_config.DelayedStartInMilliseconds == 0)
            {
                InnerStartWithManualPoll(immediatePoll);
            }
            else
            {
                Task.Factory.StartNew(() =>
                {
                    Thread.Sleep(_config.DelayedStartInMilliseconds);
                    InnerStartWithManualPoll(immediatePoll);
                });
            }
        }

        private void InnerStartWithManualPoll(Boolean immediatePoll)
        {
            Init();
            foreach (var client in _clients)
            {
                client.StartManualPolling(GetStartGlobalCheckpoint(), _config.PollingMsInterval, 4000, "CommitPollingClient");
            }
            if (immediatePoll) Poll();
        }

        void StartPolling()
        {
            Init();
            foreach (var bucket in _bucketToClient)
            {
                bucket.Value.StartAutomaticPolling(
                    GetStartGlobalCheckpoint(),
                    _config.PollingMsInterval,
                    bucket.Key.BufferSize,
                    "CommitPollingClient-" + bucket.Key.Slots.Aggregate((s1, s2) => s1 + ',' + s2));
            }
        }

        private void Init()
        {
            _maxDispatchedCheckpoint = 0;
            DumpProjections();

            TenantContext.Enter(_config.TenantId);

            _housekeeper.Init();

            _eventstore = Wireup
                .Init()
                .LogTo(t => new NEventStoreLog4NetLogger(LoggerFactory.Create(t)))
                .UsingMongoPersistence(() => _config.EventStoreConnectionString, new DocumentObjectSerializer())
                .InitializeStorageEngine()
                .Build();

            ConfigureProjections();

            // cleanup
            _housekeeper.RemoveAll(_eventstore.Advanced);

            var allSlots = _projectionsBySlot.Keys.ToArray();

            //recreate all polling clients.
            foreach (var bucket in _config.BucketInfo)
            {
                var client = _pollingClientFactory(_eventstore.Advanced);
                _clients.Add(client);
                _bucketToClient.Add(bucket, client);
            }

            foreach (var slotName in allSlots)
            {
                MetricsHelper.CreateMeterForDispatcherCountSlot(slotName);
                var startCheckpoint = GetStartCheckpointForSlot(slotName);
                Logger.InfoFormat("Slot {0} starts from {1}", slotName, startCheckpoint);

                var name = slotName;
                //find right consumer
                var slotBucket = _config.BucketInfo.SingleOrDefault(b =>
                    b.Slots.Any(s => s.Equals(slotName, StringComparison.OrdinalIgnoreCase))) ??
                    _config.BucketInfo.Single(b => b.Slots[0] == "*");
                var client = _bucketToClient[slotBucket];
                client.AddConsumer(commit => DispatchCommit(commit, name, LongCheckpoint.Parse(startCheckpoint)));
            }

            MetricsHelper.SetProjectionEngineCurrentDispatchCount(() => _countOfConcurrentDispatchingCommit);
        }

        string GetStartGlobalCheckpoint()
        {
            var checkpoints = _projectionsBySlot.Keys.Select(GetStartCheckpointForSlot).Distinct().ToArray();
            var min = checkpoints.Any() ? checkpoints.Min(x => LongCheckpoint.Parse(x).LongValue) : 0;

            return new LongCheckpoint(min).Value;
        }

        string GetStartCheckpointForSlot(string slotName)
        {
            LongCheckpoint min = null;

            var projections = _projectionsBySlot[slotName];
            foreach (var projection in projections)
            {
                if (_checkpointTracker.NeedsRebuild(projection))
                    return null;

                var currentValue = _checkpointTracker.GetCurrent(projection);
                if (currentValue == null)
                    return null;

                var currentCheckpoint = LongCheckpoint.Parse(currentValue);

                if (min == null || currentCheckpoint.LongValue < min.LongValue)
                {
                    min = currentCheckpoint;
                }
            }

            return min.Value;
        }

        void ConfigureProjections()
        {
            _checkpointTracker.SetUp(_allProjections, 1, true);
            var lastCommit = GetLastCommitId();
            foreach (var slot in _projectionsBySlot)
            {
                var maxCheckpointDispatchedInSlot = slot.Value
                    .Select(projection =>
                    {
                        var checkpoint = _checkpointTracker.GetCheckpoint(projection);
                        var longCheckpoint = LongCheckpoint.Parse(checkpoint);
                        return longCheckpoint.LongValue;
                    })
                    .Max();

                foreach (var projection in slot.Value)
                {
                    if (_checkpointTracker.NeedsRebuild(projection))
                    {
                        //Check if this slot has ever dispatched at least one commit
                        if (maxCheckpointDispatchedInSlot > 0)
                        {
                            _checkpointTracker.SetCheckpoint(projection.GetCommonName(),
                                maxCheckpointDispatchedInSlot.ToString());
                            projection.Drop();
                            projection.StartRebuild(_rebuildContext);
                            _checkpointTracker.RebuildStarted(projection, lastCommit);
                        }
                        else
                        {
                            //this is a new slot, all the projection should execute
                            //as if they are not in rebuild, because they are new
                            //so we need to immediately stop rebuilding.
                            projection.StopRebuild();
                        }
                    }

                    projection.SetUp();
                }
            }
            var errors = _checkpointTracker.GetCheckpointErrors();
            if (errors.Any())
            {
                StringBuilder fullError = new StringBuilder();
                foreach (var error in errors)
                {
                    Logger.ErrorFormat("CheckpointError: {0}", error);
                    fullError.AppendLine(error);
                }
                throw new Exception(String.Format("Found {0} errors in checkpoint status: {1}", errors.Count, fullError.ToString()));
            }
        }

        private String GetLastCommitId()
        {
            var collection = GetMongoCommitsCollection();
            var lastCommit = collection
                .Find(Builders<BsonDocument>.Filter.Gte("_id", 0))
                .Sort(Builders<BsonDocument>.Sort.Descending("_id"))
                .Limit(1)
                .FirstOrDefault();

            if (lastCommit == null) return null;

            return lastCommit["_id"].ToString();
        }

        void HandleError(Exception exception)
        {
            Logger.Fatal("Polling stopped", exception);
        }

        void StopPolling()
        {
            foreach (var client in _clients)
            {
                client.StopPolling(false);
            }
            _clients.Clear();
            _bucketToClient.Clear();
            if (_eventstore != null)
            {
                _eventstore.Dispose();
            }
        }

        private ConcurrentDictionary<String, Int64> lastCheckpointDispatched =
            new ConcurrentDictionary<string, long>();

        void DispatchCommit(ICommit commit, string slotName, LongCheckpoint startCheckpoint)
        {
            Interlocked.Increment(ref _countOfConcurrentDispatchingCommit);
            //Console.WriteLine("[{0:00}] - Slot {1}", Thread.CurrentThread.ManagedThreadId, slotName);
            if (Logger.IsDebugEnabled) Logger.ThreadProperties["commit"] = commit.CommitId;

            if (Logger.IsDebugEnabled) Logger.DebugFormat("Dispatching checkpoit {0} on tenant {1}", commit.CheckpointToken, _config.TenantId);
            TenantContext.Enter(_config.TenantId);
            var chkpoint = LongCheckpoint.Parse(commit.CheckpointToken);

            if (!lastCheckpointDispatched.ContainsKey(slotName))
            {
                lastCheckpointDispatched[slotName] = 0;
            }
            if (!(lastCheckpointDispatched[slotName] < chkpoint.LongValue))
            {
                var error = String.Format("Sequence broken, last checkpoint for slot {0} was {1} and now we dispatched {2}",
                        slotName, lastCheckpointDispatched[slotName], chkpoint.LongValue);
                Logger.Error(error);
                throw new Exception(error);
            }

            if (lastCheckpointDispatched[slotName] + 1 != chkpoint.LongValue)
            {
                if (lastCheckpointDispatched[slotName] > 0)
                {
                    Logger.DebugFormat("Sequence of commit non consecutive, last dispatched {0} receiving {1}",
                      lastCheckpointDispatched[slotName], chkpoint.LongValue);
                }
            }
            lastCheckpointDispatched[slotName] = chkpoint.LongValue;


            if (chkpoint.LongValue <= startCheckpoint.LongValue)
            {
                //Already dispatched, skip it.
                Interlocked.Decrement(ref _countOfConcurrentDispatchingCommit);
                return;
            }

            var projections = _projectionsBySlot[slotName];

            bool dispatchCommit = false;
            var eventCount = commit.Events.Count();

            var projectionToUpdate = new List<string>();

            for (int index = 0; index < eventCount; index++)
            {
                var eventMessage = commit.Events.ElementAt(index);
                try
                {
                    var evt = (DomainEvent)eventMessage.Body;
                    if (Logger.IsDebugEnabled) Logger.ThreadProperties["evType"] = evt.GetType().Name;
                    if (Logger.IsDebugEnabled) Logger.ThreadProperties["evMsId"] = evt.MessageId;
                    if (Logger.IsDebugEnabled) Logger.ThreadProperties["evCheckpointToken"] = commit.CheckpointToken;
                    string eventName = evt.GetType().Name;
                    foreach (var projection in projections)
                    {
                        var cname = projection.GetCommonName();
                        if (Logger.IsDebugEnabled) Logger.ThreadProperties["prj"] = cname;
                        var checkpointStatus = _checkpointTracker.GetCheckpointStatus(cname, commit.CheckpointToken);

                        bool handled;
                        long ticks = 0;

                        try
                        {
                            //pay attention, stopwatch consumes time.
                            var sw = new Stopwatch();
                            sw.Start();
                            handled = projection.Handle(evt, checkpointStatus.IsRebuilding);
                            sw.Stop();
                            ticks = sw.ElapsedTicks;
                            MetricsHelper.IncrementProjectionCounter(cname, slotName, eventName, ticks);
                        }
                        catch (Exception ex)
                        {
                            Logger.FatalFormat(ex, "[Slot: {3} Projection: {4}] Failed checkpoint: {0} StreamId: {1} Event Name: {2}",
                                commit.CheckpointToken,
                                commit.StreamId,
                                eventName,
                                slotName,
                                cname
                            );
                            throw;
                        }

                        if (handled)
                        {
                            _metrics.Inc(cname, eventName, ticks);

                            if (Logger.IsDebugEnabled)
                                Logger.DebugFormat("[{3}] [{4}] Handled checkpoint {0}: {1} > {2}",
                                    commit.CheckpointToken,
                                    commit.StreamId,
                                    eventName,
                                    slotName,
                                    cname
                                );
                        }

                        if (!checkpointStatus.IsRebuilding)
                        {
                            if (handled)
                                dispatchCommit = true;

                            projectionToUpdate.Add(cname);
                        }
                        else
                        {
                            if (checkpointStatus.IsLast && (index == eventCount - 1))
                            {
                                projection.StopRebuild();
                                var meter = _metrics.GetMeter(cname);
                                _checkpointTracker.RebuildEnded(projection, meter);

                                Logger.InfoFormat("Rebuild done {0} @ {1}",
                                    projection.GetType().FullName,
                                    commit.CheckpointToken
                                );
                            }
                        }
                        if (Logger.IsDebugEnabled) Logger.ThreadProperties["prj"] = null;
                    }

                    ClearLoggerThreadPropertiesForEventDispatchLoop();
                }
                catch (Exception ex)
                {
                    Logger.ErrorFormat(ex, "Error dispathing commit id: {0}\nMessage: {1}\nError: {2}",
                        commit.CheckpointToken, eventMessage.Body, ex.Message);
                    ClearLoggerThreadPropertiesForEventDispatchLoop();
                    throw;
                }
            }

            if (projectionToUpdate.Count == 0)
            {
                //I'm in rebuilding or no projection had run any events, only update slot
                _checkpointTracker.UpdateSlot(slotName, commit.CheckpointToken);
            }
            else
            {
                //I'm not on rebuilding, we have projection to update, update everything with one call.
                _checkpointTracker.UpdateSlotAndSetCheckpoint(slotName, projectionToUpdate, commit.CheckpointToken);
            }
            MetricsHelper.MarkCommitDispatchedCount(slotName, 1);

            if (dispatchCommit)
                _notifyCommitHandled.SetDispatched(commit);

            // ok in multithread wihout locks!
            if (_maxDispatchedCheckpoint < chkpoint.LongValue)
            {
                if (Logger.IsDebugEnabled)
                {
                    Logger.DebugFormat("Updating last dispatched checkpoint from {0} to {1}",
                        _maxDispatchedCheckpoint,
                        chkpoint.LongValue
                    );
                }
                _maxDispatchedCheckpoint = chkpoint.LongValue;
            }

            if (Logger.IsDebugEnabled) Logger.ThreadProperties["commit"] = null;
            Interlocked.Decrement(ref _countOfConcurrentDispatchingCommit);
        }

        private void ClearLoggerThreadPropertiesForEventDispatchLoop()
        {
            if (Logger.IsDebugEnabled) Logger.ThreadProperties["evType"] = null;
            if (Logger.IsDebugEnabled) Logger.ThreadProperties["evMsId"] = null;
            if (Logger.IsDebugEnabled) Logger.ThreadProperties["evCheckpointToken"] = null;
            if (Logger.IsDebugEnabled) Logger.ThreadProperties["prj"] = null;
        }

        public void Update()
        {
            if (Logger.IsDebugEnabled) Logger.DebugFormat("Update triggered on tenant {0}", _config.TenantId);
            Poll();
        }

        public async Task UpdateAndWait()
        {
            if (Logger.IsDebugEnabled) Logger.DebugFormat("Polling from {0}", _config.EventStoreConnectionString);
            var collection = GetMongoCommitsCollection();
            int retryCount = 0;
            long lastDispatchedCheckpoint = 0;

            while (true)
            {
                var last = collection  
                    .FindAll()            
                    .Sort(Builders<BsonDocument>.Sort.Descending("_id"))
                    .Limit(1)
                    .Project(Builders<BsonDocument>.Projection.Include("_id"))
                    .FirstOrDefault();

                if (last != null)
                {
                    var checkpointId = last["_id"].AsInt64;
                    if (Logger.IsDebugEnabled) Logger.DebugFormat("Last checkpoint is {0}", checkpointId);

                    Poll();
                    await Task.Delay(1000);

                    var dispatched = _maxDispatchedCheckpoint;
                    if (Logger.IsDebugEnabled)
                    {
                        Logger.DebugFormat(
                            "Checkpoint {0} : Max dispatched {1}",
                            checkpointId, dispatched
                            );
                    }
                    if (checkpointId == dispatched)
                    {
                        last =  collection
                            .FindAll()
                            .Sort(Builders<BsonDocument>.Sort.Descending("_id"))
                            .Limit(1)
                            .Project(Builders<BsonDocument>.Projection.Include("_id"))
                            .FirstOrDefault();

                        checkpointId = last["_id"].AsInt64;
                        if (checkpointId == dispatched)
                        {
                            if (Logger.IsDebugEnabled) Logger.Debug("Polling done!");
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
                                throw new Exception("Polling failed, engine freezed at checkpoint " + dispatched);
                            }
                        }
                    }
                }
                else
                {
                    if (Logger.IsDebugEnabled) Logger.Debug("Event store is empty");
                    return;
                }
            }
        }

        private IMongoCollection<BsonDocument> GetMongoCommitsCollection()
        {
            var url = new MongoUrl(_config.EventStoreConnectionString);
            var client = new MongoClient(url);
            var db = client.GetDatabase(url.DatabaseName);
            var collection = db.GetCollection<BsonDocument>("Commits");
            return collection;
        }
    }

    public class BucketInfo
    {
        public String[] Slots { get; set; }

        public Int32 BufferSize { get; set; }
    }
}
