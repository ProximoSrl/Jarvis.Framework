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
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using Jarvis.Framework.Kernel.Support;
using System.Collections.Concurrent;
using Metrics;
using NStore.Persistence.Mongo;
using Jarvis.Framework.Kernel.Engine;
using Jarvis.Framework.Shared.Exceptions;
using NStore.Core.Persistence;
using NStore.Domain;
using Jarvis.Framework.Shared.Logging;
using Jarvis.Framework.Shared.Support;
using Jarvis.Framework.Shared.Messages;

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

		private readonly ProjectionMetrics _metrics;
		private long _maxDispatchedCheckpoint = 0;
		private Int32 _countOfConcurrentDispatchingCommit = 0;

		private readonly ILogger _logger;

		private readonly ILoggerThreadContextManager _loggerThreadContextManager;

		public ILoggerFactory LoggerFactory { get; set; }

		private IPersistence _persistence;
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

		public ProjectionEngine(
			ICommitPollingClientFactory pollingClientFactory,
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

			_logger = logger;
			_loggerThreadContextManager = loggerThreadContextManager;

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

			_allProjections = projections;
			_projectionsBySlot = projections
				.GroupBy(x => x.Info.SlotName)
				.ToDictionary(x => x.Key, x => x.OrderByDescending(p => p.Priority).ToArray());

			_metrics = new ProjectionMetrics(_allProjections);
			_bucketToClient = new Dictionary<BucketInfo, ICommitPollingClient>();

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
		}

		private async Task InitAsync()
		{
			_maxDispatchedCheckpoint = 0;
			DumpProjections();

			TenantContext.Enter(_config.TenantId);

			await _housekeeper.InitAsync().ConfigureAwait(false);

			var mongoStoreOptions = new MongoPersistenceOptions
			{
				PartitionsConnectionString = _config.EventStoreConnectionString,
				UseLocalSequence = true,
				PartitionsCollectionName = EventStoreFactory.PartitionCollectionName,
				SequenceCollectionName = "event_sequence",
				DropOnInit = false
			};

			var mongoPersistence = new MongoPersistence(mongoStoreOptions);

			await mongoPersistence.InitAsync(CancellationToken.None).ConfigureAwait(false);

			_persistence = mongoPersistence;

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

		Int64 GetStartGlobalCheckpoint()
		{
			var checkpoints = _projectionsBySlot.Keys.Select(GetStartCheckpointForSlot).Distinct().ToArray();
			return checkpoints.Any() ? checkpoints.Min() : 0;
		}

		Int64 GetStartCheckpointForSlot(string slotName)
		{
			Int64 min = Int64.MaxValue;

			var projections = _projectionsBySlot[slotName];
			foreach (var projection in projections)
			{
				if (_checkpointTracker.NeedsRebuild(projection))
					throw new JarvisFrameworkEngineException("Projection engine is not used anymore for rebuild. Rebuild is done with the specific RebuildProjectionEngine");

				var currentValue = _checkpointTracker.GetCurrent(projection);
				if (currentValue < min)
				{
					min = currentValue;
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

		void StopPolling()
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
		}

		private readonly ConcurrentDictionary<String, Int64> lastCheckpointDispatched =
			new ConcurrentDictionary<string, long>();

		private async Task DispatchCommitAsync(IChunk chunk, string slotName, Int64 startCheckpoint)
		{
			Interlocked.Increment(ref _countOfConcurrentDispatchingCommit);

			using (_loggerThreadContextManager.SetDisposableContextProperty("commit", $"{chunk.OperationId}/{chunk.Position}"))
			{
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

				bool dispatchCommit = false;

				object[] events = GetArrayOfObjectFromChunk(chunk);

				var eventCount = events.Length;

				var projectionToUpdate = new List<string>();

				for (int index = 0; index < eventCount; index++)
				{
					var eventMessage = events[index];
					try
					{
						var message = eventMessage as IMessage;

						DisposableStack serilogcontext = new DisposableStack();
						string eventName = eventMessage.GetType().Name;

						serilogcontext.Push(_loggerThreadContextManager.SetDisposableContextProperty("evType", eventName));
						serilogcontext.Push(_loggerThreadContextManager.SetDisposableContextProperty("evMsId", message?.MessageId));
						serilogcontext.Push(_loggerThreadContextManager.SetDisposableContextProperty("evCheckpointToken", chunk.Position));

						using (serilogcontext)
						{
							foreach (var projection in projections)
							{
								var cname = projection.Info.CommonName;
								using (_loggerThreadContextManager.SetDisposableContextProperty("prj", cname))
								{
									var checkpointStatus = _checkpointTracker.GetCheckpointStatus(cname, chunk.Position);
									if (checkpointStatus.IsRebuilding)
										throw new JarvisFrameworkEngineException($"Error in projection engine, position {chunk.Position} for projection {projection.Info.CommonName} have IsRebuilding to true. This is not admitted");

									bool handled;
									long ticks = 0;

									try
									{
										//pay attention, stopwatch consumes time.
										var sw = new Stopwatch();
										sw.Start();
										handled = await projection.HandleAsync(eventMessage, false).ConfigureAwait(false);
										sw.Stop();
										ticks = sw.ElapsedTicks;
										MetricsHelper.IncrementProjectionCounter(cname, slotName, eventName, ticks);
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
										_engineFatalErrors.Add(error);
										throw;
									}

									if (handled)
									{
										_metrics.Inc(cname, eventName, ticks);

										if (_logger.IsDebugEnabled)
											_logger.DebugFormat("[{3}] [{4}] Handled checkpoint {0}: {1} > {2}",
												chunk.Position,
												chunk.PartitionId,
												eventName,
												slotName,
												cname
											);
									}

									if (handled)
										dispatchCommit = true;

									projectionToUpdate.Add(cname);
								}
							}
						}
					}
					catch (Exception ex)
					{
						_logger.ErrorFormat(ex, "Error dispathing Chunk [{0}]\n Message: {1}\n Error: {2}\n",
							chunk.Position, eventMessage?.GetType()?.Name, ex.Message);
						throw;
					}
				}

				//TODO NSTORE: We removed the ability to use standard projection engine with rebuild, but we still need to support the
				//continuous rebuidl mode probably.
				if (projectionToUpdate.Count == 0 && RebuildSettings.ContinuousRebuild == false)
				{
					//I'm in rebuilding or no projection had run any events, only update slot
					_checkpointTracker.UpdateSlot(slotName, chunk.Position);
				}
				else
				{
					//I'm not on rebuilding or we are in continuous rebuild., we have projection to update, update everything with one call.
					_checkpointTracker.UpdateSlotAndSetCheckpoint(slotName, projectionToUpdate, chunk.Position);
				}

				//TODO: Find a better way, for now we signal projection that we fully dispatched a checkpoint token
				//all events are dispatched.
				foreach (var projection in projections)
				{
					projection.CheckpointProjected(chunk.Position);
				}

				MetricsHelper.MarkCommitDispatchedCount(slotName, 1);

				if (dispatchCommit)
				{
					await _notifyCommitHandled.SetDispatched(slotName, chunk).ConfigureAwait(false);
				}

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
			}
			Interlocked.Decrement(ref _countOfConcurrentDispatchingCommit);
		}

		private static object[] GetArrayOfObjectFromChunk(IChunk chunk)
		{
			Object[] events;

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
