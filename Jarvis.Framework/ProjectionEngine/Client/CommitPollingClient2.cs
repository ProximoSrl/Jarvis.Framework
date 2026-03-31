using Castle.Core.Logging;
using Jarvis.Framework.Kernel.Support;
using Jarvis.Framework.Shared.Exceptions;
using Jarvis.Framework.Shared.HealthCheck;
using Jarvis.Framework.Shared.Store;
using MongoDB.Driver;
using NStore.Core.Logging;
using NStore.Core.Persistence;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Jarvis.Framework.Kernel.ProjectionEngine.Client
{
    /// <summary>
    /// This was completely rewritten for NEventstore 6 beta then
    /// completely re-adapted for NStore.
    /// </summary>
    public class CommitPollingClient2 : ICommitPollingClient
    {
        private int _bufferSize = 4000;

        private readonly ICommitEnhancer _enhancer;
        private readonly String _id;

        private readonly ILogger _logger;

        public CommitPollingClientStatus Status { get; private set; }

        public Exception LastException { get; private set; }

        private PollingClient _innerClient;
        private JarvisFrameworkLambdaSubscription _innerSubscription;
        private readonly IPersistence _persistence;

        public CommitPollingClient2(
            IPersistence persistStreams,
            ICommitEnhancer enhancer,
            String id,
            ILogger logger,
            INStoreLoggerFactory factory)
        {
            _id = id;
            _factory = factory;
            _enhancer = enhancer ?? throw new ArgumentNullException(nameof(enhancer));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _persistence = persistStreams ?? throw new ArgumentNullException(nameof(persistStreams));

            _consumers = new List<ITargetBlock<IChunk>>();
            Status = CommitPollingClientStatus.Stopped;

            _logger.InfoFormat("Created Commit Polling client id: {0}", id);
        }

        public void AddConsumer(String consumerId, Func<IChunk, Task> consumerAction)
        {
            ExecutionDataflowBlockOptions consumerOptions = new ExecutionDataflowBlockOptions();
            consumerOptions.BoundedCapacity = _bufferSize;
            ConsumerActionWrapper wrapper = new ConsumerActionWrapper(consumerId, consumerAction, this);
            var actionBlock = new ActionBlock<IChunk>((Func<IChunk, Task>)wrapper.Consume, consumerOptions);

            _consumers.Add(actionBlock);
        }

        #region Dispatching

        private readonly List<ITargetBlock<IChunk>> _consumers;

        private class ConsumerActionWrapper
        {
            private readonly Func<IChunk, Task> _consumerAction;
            private Boolean _active;
            private String _error;
            private int _consecutiveTransientErrors;

            private readonly CommitPollingClient2 _owner;
            private readonly String _consumerId;

            public ConsumerActionWrapper(String consumerId, Func<IChunk, Task> consumerAction, CommitPollingClient2 owner)
            {
                _consumerAction = consumerAction;
                _active = true;
                _owner = owner;
                _consumerId = consumerId;

                JarvisFrameworkHealthChecks.RegisterHealthCheck("PollingConsumer-" + consumerId, () =>
                {
                    if (string.IsNullOrEmpty(_error))
                    {
                        return JarvisFrameworkHealthCheckResult.Healthy($"Consumer {_consumerId} healthy - LastDispatched: {_owner.LastDispatchedPosition}");
                    }

                    return JarvisFrameworkHealthCheckResult.Unhealthy($"Consumer {_consumerId} error: {_error} - LastDispatched: {_owner.LastDispatchedPosition}");
                });
            }

            public string Error { get { return _error; } }
            public Boolean Active { get { return _active; } }

            public async Task Consume(IChunk chunk)
            {
                if (!Active)
                {
                    return; //consumer failed, we need to stop dispatching further commits.
                }

                while (true)
                {
                    try
                    {
                        await _consumerAction(chunk).ConfigureAwait(false);

                        // Reset transient error counter on success and clear any previous transient error message
                        if (_consecutiveTransientErrors > 0)
                        {
                            _owner._logger.InfoFormat("CommitPollingClient {0}: Consumer {1} recovered after {2} transient errors",
                                _owner._id,
                                _consumerId,
                                _consecutiveTransientErrors);
                            _consecutiveTransientErrors = 0;
                            _error = null;
                        }

                        return;
                    }
                    catch (Exception ex) when (IsMongoTransientException(ex))
                    {
                        _consecutiveTransientErrors++;
                        var delay = GetTransientRetryDelay(_consecutiveTransientErrors);

                        _owner._logger.WarnFormat(ex, "CommitPollingClient {0}: Transient error in consumer {1} for chunk {2} (attempt {3}), retrying in {4}ms: {5}",
                            _owner._id,
                            _consumerId,
                            chunk.Position,
                            _consecutiveTransientErrors,
                            delay,
                            ex.Message);

                        _error = $"Transient error dispatching {chunk.Position} (attempt {_consecutiveTransientErrors}): {ex.Message}";

                        await Task.Delay(delay).ConfigureAwait(false);
                        // Loop back and retry
                    }
                    catch (Exception ex)
                    {
                        _owner._logger.ErrorFormat(ex, "CommitPollingClient {0}: Error during Commit consumer {1}: {2}. Will retry in 10 minutes.",
                            _owner._id,
                            _consumerId,
                            ex.Message);
                        _error = $"Error dispatching {chunk.Position} (will retry in 10 min): {ex.Message}";

                        // For any non-transient exception, retry every 10 minutes — the error
                        // may still be temporary (e.g. network partition, DNS, timeout).
                        await Task.Delay(600_000).ConfigureAwait(false);
                        // Loop back and retry
                    }
                }
            }

            /// <summary>
            /// Returns the retry delay based on number of consecutive transient errors:
            /// - Attempts 1-5: 1 second (brief interruption)
            /// - Attempts 6-15: 10 seconds (longer outage)
            /// - Attempts 16+: 60 seconds (extended outage)
            /// </summary>
            private static int GetTransientRetryDelay(int attempt)
            {
                if (attempt <= 5) return 1000;
                if (attempt <= 15) return 10_000;
                return 60_000;
            }

            /// <summary>
            /// Determines if a MongoDB exception is transient (infrastructure-level)
            /// and the operation could succeed if retried after a short delay.
            /// </summary>
            private static bool IsMongoTransientException(Exception ex)
            {
                // Walk the exception chain to find MongoDB transient exceptions
                var current = ex;
                while (current != null)
                {
                    if (current is MongoConnectionException
                        || current is MongoConnectionPoolPausedException
                        || current is MongoNotPrimaryException
                        || current is MongoNodeIsRecoveringException)
                    {
                        return true;
                    }

                    // MongoCommandException with transient labels
                    if (current is MongoException mongoEx && mongoEx.HasErrorLabel("TransientTransactionError"))
                    {
                        return true;
                    }

                    current = current.InnerException;
                }

                return false;
            }
        }

        #endregion

        #region Polling

        private Int64 _lastDispatchedPosition;

        /// <summary>
        /// Allow from outside to read last dispatched position
        /// </summary>
        public Int64 LastDispatchedPosition => _lastDispatchedPosition;

        private readonly CancellationTokenSource _stopRequested = new CancellationTokenSource();

        public void Configure(
            Int64 checkpointTokenFrom,
            Int32 bufferSize)
        {
            _logger.InfoFormat("CommitPollingClient {0}: Configured starting from {1} buffer {2}", _id, checkpointTokenFrom, bufferSize);
            _bufferSize = bufferSize;

            _lastDispatchedPosition = checkpointTokenFrom;

            LastException = null;
            //prepare single poller thread.
            CreateTplChain();

            _innerSubscription = new JarvisFrameworkLambdaSubscription(DispatchChunk, _id);

            _innerClient = new PollingClient(
                _persistence,
                checkpointTokenFrom,
                _innerSubscription,
                _factory);

            RegisterHealthChecks(_id);
        }

        public void Start(int pollingIntervalInMilliseconds)
        {
            _logger.InfoFormat("CommitPollingClient {0}: start called", _id);
            if (Status != CommitPollingClientStatus.Polling)
            {
                if (_innerClient == null)
                {
                    throw new JarvisFrameworkEngineException($"CommitPollingClient {_id}: Cannot start polling client because you forget to call Configure First");
                }

                _innerClient.PollingIntervalMilliseconds = pollingIntervalInMilliseconds;
                _innerClient.Start();
                Status = CommitPollingClientStatus.Polling;
            }
            else
            {
                _logger.WarnFormat("CommitPollingClient {0}: Start called but client was in status {1}", _id, Status);
            }
        }

        private void RegisterHealthChecks(string pollerName)
        {
            JarvisFrameworkKernelMetricsHelper.SetCommitPollingClientBufferSize(pollerName, () => GetClientBufferSize());
            //Set health check for polling
            JarvisFrameworkHealthChecks.RegisterHealthCheck("Polling-" + pollerName, () =>
            {
                if (Status == CommitPollingClientStatus.Stopped)
                {
                    //poller is stopped, system healty
                    return JarvisFrameworkHealthCheckResult.Healthy("Automatic polling stopped");
                }
                else if (Status == CommitPollingClientStatus.Faulted || LastException != null)
                {
                    //poller is stopped, system is not healty anymore.
                    var exceptionText = (LastException != null ? LastException.ToString() : "");
                    exceptionText = exceptionText.Replace("{", "{{").Replace("}", "}}");
                    return JarvisFrameworkHealthCheckResult.Unhealthy(
                        "[LastDispatchedPosition: {0}] - Faulted (exception in consumer): {1}",
                        _lastDispatchedPosition,
                        exceptionText);
                }

                return JarvisFrameworkHealthCheckResult.Healthy($"Poller alive / last dispatched position {LastDispatchedPosition}");
            });

            //Now register health check for the internal NES poller, to diagnose errors in polling.
            JarvisFrameworkHealthChecks.RegisterHealthCheck("Polling internal errors: ", () =>
            {
                if (_innerSubscription?.LastException != null)
                {
                    return JarvisFrameworkHealthCheckResult.Unhealthy("[LastDispatchedPosition: {0}] - Inner NStore poller has error: {1}",
                        _lastDispatchedPosition,
                        _innerSubscription?.LastException);
                }
                return JarvisFrameworkHealthCheckResult.Healthy("Inner NES Poller Ok");
            });
        }

        public void Stop(Boolean setToFaulted)
        {
            _logger.InfoFormat("CommitPollingClient {0}: Stop called, set to faulted {1}", _id, setToFaulted);
            if (Status == CommitPollingClientStatus.Polling)
            {
                if (_innerClient == null)
                {
                    throw new JarvisFrameworkEngineException($"CommitPollingClient {_id}: Cannot stop polling client because you forget to call Configure First");
                }

                if (Status != CommitPollingClientStatus.Polling)
                {
                    _logger.WarnFormat($"CommitPollingClient {0}: Cannot stop poller poller is in unstoppable status: {1}", _id, Status);
                    return;
                }

                _innerClient.Stop();
                CloseEverything();
                Status = setToFaulted ?
                    CommitPollingClientStatus.Faulted :
                    CommitPollingClientStatus.Stopped;
            }
            else
            {
                _logger.WarnFormat("CommitPollingClient {0}: Stop called but it was not started", _id);
            }
        }

        private void CloseEverything()
        {
            _stopRequested.Cancel();
            CloseTplChain();
        }

        /// <summary>
        /// Add a manual poll command, this is an async function so it return
        /// Task.
        /// </summary>
        public Task PollAsync()
        {
            //Add poll command if there are no other poll command in queuel
            return _innerClient.Poll();
        }

        private double GetClientBufferSize()
        {
            if (_buffer != null)
            {
                return _buffer.Count;
            }

            return 0;
        }

        private async Task<Boolean> DispatchChunk(IChunk chunk)
        {
            try
            {
                if (_logger.IsDebugEnabled)
                {
                    _logger.DebugFormat("CommitPollingClient {0}: CommitPollingClient2: Dispatched chunk {1}", _id, chunk.Position);
                }

                if (_stopRequested.IsCancellationRequested)
                {
                    CloseTplChain();
                    return false;
                }

                if (chunk.PartitionId.StartsWith("system.empty"))
                {
                    if (_logger.IsDebugEnabled)
                    {
                        _logger.DebugFormat("CommitPollingClient {0}: CommitPollingClient2: Found empty commit - {1}", _id, chunk.Position);
                    }
                }
                else
                {
                    _enhancer.Enhance(chunk);
                }

                var task = _buffer.SendAsync(chunk);

                //TODO: CHECK if this is really needed, we could simply await
                //This was introduced because if you cancel the polling but the
                //mesh is full and the projections are stopped the mesh will never
                //accept the message again.
                //wait till the commit is stored in tpl queue
                while (!task.Wait(2000))
                {
                    //maybe the mesh is full, but check for completion
                    if (_stopRequested.IsCancellationRequested)
                    {
                        _buffer.Complete();
                        return false;
                    }
                }

                //Check if block postponed the message, if for some
                //reason the message is postponed, exit from the polling
                var result = await task;
                if (!result)
                {
                    //TPL is somewhat stuck
                    _logger.ErrorFormat("CommitPollingClient {0}: Unable to dispatch commit {1} into TPL chain, buffer block did not accept message", _id, chunk.Position);
                    _stopRequested.Cancel();
                    return false; //stop polling.
                }

                _lastDispatchedPosition = chunk.Position;
                return true;
            }
            catch (Exception ex)
            {
                _logger.ErrorFormat(ex, "CommitPollingClient2 {0}: Error in dispatching commit {1} - {2}", _id, chunk.Position, ex.Message);
                return false;
            }
        }

        #endregion

        #region Tpl

        private BufferBlock<IChunk> _buffer;
        private ITargetBlock<IChunk> _broadcaster;
        private readonly INStoreLoggerFactory _factory;

        private void CreateTplChain()
        {
            DataflowBlockOptions bufferOptions = new DataflowBlockOptions();
            bufferOptions.BoundedCapacity = _bufferSize;
            _buffer = new BufferBlock<IChunk>(bufferOptions);

            ExecutionDataflowBlockOptions executionOption = new ExecutionDataflowBlockOptions();
            executionOption.BoundedCapacity = _bufferSize;

            _broadcaster = GuaranteedDeliveryBroadcastBlock.Create(_consumers, _id, 3000, meterName: _id);

            _buffer.LinkTo(_broadcaster, new DataflowLinkOptions() { PropagateCompletion = true });
        }

        private void CloseTplChain()
        {
            //signal Tpl to complete buffer.
            _buffer.Complete();
            _broadcaster.Complete();
        }

        #endregion
    }
}
