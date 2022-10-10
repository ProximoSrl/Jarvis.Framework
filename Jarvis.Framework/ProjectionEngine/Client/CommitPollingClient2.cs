using Castle.Core.Logging;
using Jarvis.Framework.Kernel.Support;
using Jarvis.Framework.Shared.Exceptions;
using Jarvis.Framework.Shared.HealthCheck;
using Jarvis.Framework.Shared.Store;
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
            if (persistStreams == null)
            {
                throw new ArgumentNullException(nameof(persistStreams));
            }

            if (enhancer == null)
            {
                throw new ArgumentNullException(nameof(enhancer));
            }

            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }

            _enhancer = enhancer;
            _consumers = new List<ITargetBlock<IChunk>>();
            _logger = logger;
            _id = id;
            Status = CommitPollingClientStatus.Stopped;
            _persistence = persistStreams;
            _factory = factory;

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

                try
                {
                    await _consumerAction(chunk).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _owner._logger.ErrorFormat(ex, "CommitPollingClient {0}: Error during Commit consumer {1}: {2} ",
                        _owner._id,
                        _consumerId,
                        ex.Message);
                    _active = false; //no more dispatch to this consumer.
                    _error = $"Error dispatching {chunk.Position}: {ex.ToString()}";

                    //it does not throw, simply stops dispatching commit to this consumer that will be blocked
                }
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

        public void Start()
        {
            _logger.InfoFormat("CommitPollingClient {0}: start called", _id);
            if (Status != CommitPollingClientStatus.Polling)
            {
                if (_innerClient == null)
                {
                    throw new JarvisFrameworkEngineException($"CommitPollingClient {_id}: Cannot start polling client because you forget to call Configure First");
                }

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

        private Task<Boolean> DispatchChunk(IChunk chunk)
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
                    return Task.FromResult(false);
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
                        return Task.FromResult(false);
                    }
                }

                //Check if block postponed the message, if for some
                //reason the message is postponed, exit from the polling
                if (!task.Result)
                {
                    //TPL is somewhat stuck
                    _logger.ErrorFormat("CommitPollingClient {0}: Unable to dispatch commit {1} into TPL chain, buffer block did not accept message", _id, chunk.Position);
                    _stopRequested.Cancel();
                    return Task.FromResult(false); //stop polling.
                }

                _lastDispatchedPosition = chunk.Position;
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _logger.ErrorFormat(ex, "CommitPollingClient2 {0}: Error in dispatching commit {1} - {2}", _id, chunk.Position, ex.Message);
                return Task.FromResult(false);
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

            _broadcaster = GuaranteedDeliveryBroadcastBlock.Create(_consumers, _id, 3000);

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
