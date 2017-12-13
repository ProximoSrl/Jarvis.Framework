using Castle.Core.Logging;
using NStore.Core.Logging;
using NStore.Core.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Jarvis.Framework.Shared.Helpers
{
	/// <summary>
	/// Needed to safely read a sequence in NStore and avoid out-of-order 
	/// write. 
	/// </summary>
	public class NStoreSequencer : ISubscription
	{
		private readonly ISubscription _subscription;
		private readonly ILogger _logger;

		public long Position { get; private set; } = 0;

		public long Processed { get; private set; }

		/// <summary>
		/// Number of retry on a single hole.
		/// </summary>
		public int RetriesOnHole { get; private set; }

		private bool _stopOnHole = true;
		private readonly Int32 _timeToWaitInMilliseconds;
		private readonly Int32 _maximumWaitCountForSingleHole;
		private readonly Int64 _safePosition;

		/// <summary>
		/// Create a sequencer
		/// </summary>
		/// <param name="lastPosition"></param>
		/// <param name="subscription"></param>
		/// <param name="timeToWaitInMilliseconds">When an hole is found there is no need to hammer the database. This is 
		/// the value that we wait, in milliseconds, before retrying.</param>
		/// <param name="maximumWaitCountForSingleHole"></param>
		/// <param name="safePosition">This means that up to this position, we can tolerate holes. Useful if we scan the
		/// whole set of events, we know that there are holes, but we already know that up to a certain position we cannot
		/// have out of order.</param>
		/// <param name="logger"></param>
		public NStoreSequencer(
			long lastPosition,
			ISubscription subscription,
			Int32 timeToWaitInMilliseconds,
			Int32 maximumWaitCountForSingleHole,
			Int64 safePosition,
			ILogger logger)
		{
			Position = lastPosition;
			_subscription = subscription;
			_timeToWaitInMilliseconds = timeToWaitInMilliseconds;
			_logger = logger;
			_maximumWaitCountForSingleHole = maximumWaitCountForSingleHole;
			_safePosition = safePosition;
		}

		public Task<bool> OnNextAsync(IChunk chunk)
		{
			_logger.Debug($"OnNext {chunk.Position}");

			//Sequence if the position is greater than safe position and the position is not the
			//subsequent position in the stream.
			if (chunk.Position > _safePosition && chunk.Position != Position + 1)
			{
				if (_stopOnHole)
				{
					RetriesOnHole++;
					_logger.Debug($"Hole detected on {chunk.Position} - {RetriesOnHole}");
					if (_timeToWaitInMilliseconds > 0) Thread.Sleep(_timeToWaitInMilliseconds);
					return Task.FromResult(false);
				}
				_logger.Debug($"Skipping hole on {chunk.Position}");
			}

			RetriesOnHole = 0;
			_stopOnHole = true;
			Position = chunk.Position;
			Processed++;
			return _subscription.OnNextAsync(chunk);
		}

		public Task OnStartAsync(long indexOrPosition)
		{
			_logger.Debug($"OnStart({indexOrPosition})");

			Position = indexOrPosition - 1;
			Processed = 0;
			_stopOnHole = RetriesOnHole < _maximumWaitCountForSingleHole;
			return _subscription.OnStartAsync(indexOrPosition);
		}

		public Task CompletedAsync(long indexOrPosition)
		{
			_logger.Debug($"Completed({indexOrPosition})");

			if (Processed > 0)
			{
				Position = indexOrPosition;
			}

			return _subscription.CompletedAsync(indexOrPosition);
		}

		public Task StoppedAsync(long indexOrPosition)
		{
			_logger.Debug($"Stopped({indexOrPosition})");
			return _subscription.StoppedAsync(indexOrPosition);
		}

		public Task OnErrorAsync(long indexOrPosition, Exception ex)
		{
			return _subscription.OnErrorAsync(indexOrPosition, ex);
		}
	}
}
