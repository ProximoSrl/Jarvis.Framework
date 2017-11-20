using Castle.Core.Logging;
using Jarvis.Framework.Kernel.Events;
using Jarvis.Framework.Kernel.MultitenantSupport;
using Jarvis.Framework.Kernel.ProjectionEngine.Client;
using Jarvis.Framework.Kernel.Support;
using Jarvis.Framework.Shared.Events;
using Jarvis.Framework.Shared.Logging;
using Jarvis.Framework.Shared.Support;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Jarvis.Framework.Kernel.ProjectionEngine.Rebuild
{
	internal class RebuildProjectionSlotDispatcher
	{
		public String SlotName { get; private set; }
		private readonly ILogger _logger;
		private readonly ProjectionEngineConfig _config;
		private readonly IEnumerable<IProjection> _projections;
		private readonly ProjectionMetrics _metrics;
		private readonly Int64 _maxCheckpointDispatched;
		private Int64 _lastCheckpointRebuilded;
		private readonly ILoggerThreadContextManager _loggerThreadContextManager;

		public RebuildProjectionSlotDispatcher(
			ILogger logger,
			String slotName,
			ProjectionEngineConfig config,
			IEnumerable<IProjection> projections,
			Int64 lastCheckpointDispatched,
			ILoggerThreadContextManager loggerThreadContextManager)
		{
			SlotName = slotName;
			_logger = logger;
			_config = config;
			_projections = projections;
			_metrics = new ProjectionMetrics(projections);
			_maxCheckpointDispatched = lastCheckpointDispatched;
			_lastCheckpointRebuilded = 0;
			_loggerThreadContextManager = loggerThreadContextManager;
		}

		public Int64 LastCheckpointDispatched
		{
			get { return _maxCheckpointDispatched; }
		}

		public IEnumerable<IProjection> Projections
		{
			get { return _projections; }
		}

		public double CheckpointToDispatch { get { return LastCheckpointDispatched - _lastCheckpointRebuilded; } }

		public bool Finished { get { return _lastCheckpointRebuilded == LastCheckpointDispatched; } }

		internal async Task DispatchEventAsync(UnwindedDomainEvent unwindedEvent)
		{
			var chkpoint = unwindedEvent.CheckpointToken;
			if (chkpoint > LastCheckpointDispatched)
			{
				if (_logger.IsDebugEnabled) _logger.DebugFormat("Discharded event {0} commit {1} because last checkpoint dispatched for slot {2} is {3}.", unwindedEvent.CommitId, unwindedEvent.CheckpointToken, SlotName, _maxCheckpointDispatched);
				return;
			}

			Interlocked.Increment(ref RebuildProjectionMetrics.CountOfConcurrentDispatchingCommit);
			object objectToDispatch = unwindedEvent.GetEvent();
			DomainEvent domainEvent = objectToDispatch as DomainEvent;
			if (_logger.IsDebugEnabled)
			{
				_logger.DebugFormat("Dispatching checkpoit {0} on tenant {1}", unwindedEvent.CheckpointToken, _config.TenantId);
				_loggerThreadContextManager.SetContextProperty("commit", unwindedEvent.CommitId);
				_loggerThreadContextManager.SetContextProperty("evType", unwindedEvent.EventType);
				_loggerThreadContextManager.SetContextProperty("evMsId", domainEvent != null ? domainEvent.MessageId.ToString() : "null");
				_loggerThreadContextManager.SetContextProperty("evCheckpointToken", unwindedEvent.CheckpointToken);
			}

			TenantContext.Enter(_config.TenantId);

			try
			{
				string eventName = unwindedEvent.EventType;
				foreach (var projection in _projections)
				{
					var cname = projection.Info.CommonName;
					if (_logger.IsDebugEnabled) _loggerThreadContextManager.SetContextProperty("prj", cname);
					long ticks = 0;

					try
					{
						//pay attention, stopwatch consumes time.
						var sw = new Stopwatch();
						sw.Start();

						await projection.HandleAsync(objectToDispatch, true).ConfigureAwait(false);
						sw.Stop();
						ticks = sw.ElapsedTicks;
						MetricsHelper.IncrementProjectionCounterRebuild(cname, SlotName, eventName, ticks);
					}
					catch (Exception ex)
					{
						_logger.FatalFormat(ex, "[Slot: {3} Projection: {4}] Failed checkpoint: {0} StreamId: {1} Event Name: {2}",
							unwindedEvent.CheckpointToken,
							unwindedEvent.PartitionId,
							eventName,
							SlotName,
							cname
						);
						throw;
					}

					_metrics.Inc(cname, eventName, ticks);

					if (_logger.IsDebugEnabled)
						_logger.DebugFormat("[{3}] [{4}] Handled checkpoint {0}: {1} > {2}",
							unwindedEvent.CheckpointToken,
							unwindedEvent.PartitionId,
							eventName,
							SlotName,
							cname
						);
					if (_logger.IsDebugEnabled) _loggerThreadContextManager.ClearContextProperty("prj");
				}
			}
			catch (Exception ex)
			{
				_logger.ErrorFormat(ex, "Error dispathing commit id: {0}\nMessage: {1}\nError: {2}",
					unwindedEvent.CheckpointToken, unwindedEvent.Event, ex.Message);
				throw;
			}
			finally
			{
				if (_logger.IsDebugEnabled)
				{
					_loggerThreadContextManager.ClearContextProperty("prj");
					_loggerThreadContextManager.ClearContextProperty("commit");
					_loggerThreadContextManager.ClearContextProperty("evType");
					_loggerThreadContextManager.ClearContextProperty("evMsId");
					_loggerThreadContextManager.ClearContextProperty("evCheckpointToken");
				}
			}
			_lastCheckpointRebuilded = chkpoint;

			Interlocked.Decrement(ref RebuildProjectionMetrics.CountOfConcurrentDispatchingCommit);
		}
	}

	public class RebuildStatus
	{
		private int _bucketActiveCount = 0;
		public int BucketActiveCount
		{
			get { return _bucketActiveCount; }
		}

		public List<BucketSummaryStatus> SummaryStatus { get; set; }

		public Boolean Done { get { return BucketActiveCount == 0; } }

		public RebuildStatus()
		{
			SummaryStatus = new List<BucketSummaryStatus>();
		}

		public void AddBucket()
		{
			Interlocked.Increment(ref _bucketActiveCount);
		}

		public void BucketDone(
			String bucketDescription,
			Int64 eventsDispatched,
			Int64 durationInMilliseconds,
			Int64 totalEventsToDispatchWithoutFiltering)
		{
			Interlocked.Decrement(ref _bucketActiveCount);
			SummaryStatus.Add(new BucketSummaryStatus()
			{
				BucketDescription = bucketDescription,
				EventsDispatched = eventsDispatched,
				DurationInMilliseconds = durationInMilliseconds,
				TotalEventsToDispatchWithoutFiltering = totalEventsToDispatchWithoutFiltering,
			});
		}

		public class BucketSummaryStatus
		{
			public String BucketDescription { get; set; }

			public Int64 TotalEventsToDispatchWithoutFiltering { get; set; }

			public Int64 EventsDispatched { get; set; }

			public Int64 DurationInMilliseconds { get; set; }
		}
	}
}
