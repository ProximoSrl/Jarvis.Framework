using Jarvis.Framework.Kernel.ProjectionEngine.Client;
using Jarvis.Framework.Shared.Helpers;
using NStore.Core.Persistence;
using NStore.Domain;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Jarvis.Framework.Shared.ReadModel.Atomic
{
	/// <summary>
	/// Subscription for NStore to handle batch catchup operations where multiple readmodels
	/// of the same type need to be caught up. Each readmodel may be at a different aggregate
	/// version, and the subscription ensures that only events newer than the readmodel's
	/// current version are processed.
	/// </summary>
	/// <typeparam name="TModel">Type of atomic readmodel being caught up</typeparam>
	public sealed class AtomicReadmodelBatchCatchupSubscription<TModel> : ISubscription
		where TModel : IAtomicReadModel
	{
		private readonly ICommitEnhancer _commitEnhancer;
		private readonly IDictionary<string, TModel> _readmodels;
		private readonly Func<Changeset, Boolean> _stopCondition;

		/// <summary>
		/// Create a batch catchup subscription for readmodels of the same type.
		/// </summary>
		/// <param name="commitEnhancer">The commit enhancer to apply to chunks</param>
		/// <param name="readmodels">Dictionary mapping aggregate id to readmodel instance.
		/// Each readmodel will be updated in-place when events are processed.</param>
		/// <param name="stopCondition">Optional stop condition evaluated BEFORE processing
		/// each changeset. If null, processing continues until all events are read.</param>
		public AtomicReadmodelBatchCatchupSubscription(
			ICommitEnhancer commitEnhancer,
			IDictionary<string, TModel> readmodels,
			Func<Changeset, Boolean> stopCondition = null)
		{
			_commitEnhancer = commitEnhancer ?? throw new ArgumentNullException(nameof(commitEnhancer));
			_readmodels = readmodels ?? throw new ArgumentNullException(nameof(readmodels));
			_stopCondition = stopCondition ?? (cs => false);
		}

		/// <inheritdoc/>
		public Task CompletedAsync(long indexOrPosition)
		{
			return Task.CompletedTask;
		}

		/// <inheritdoc/>
		public Task OnErrorAsync(long indexOrPosition, Exception ex)
		{
			throw ex;
		}

		/// <inheritdoc/>
		public Task<bool> OnNextAsync(IChunk chunk)
		{
			_commitEnhancer.Enhance(chunk);
			if (chunk.Payload is Changeset cs)
			{
				if (_stopCondition(cs))
				{
					return Task.FromResult(false);
				}

				var aggregateId = cs.GetIdentity().AsString();
				if (_readmodels.TryGetValue(aggregateId, out var readmodel))
				{
					// Only process if this changeset is newer than the readmodel's current version.
					// This is the key difference from AtomicReadmodelMultiAggregateSubscription:
					// each readmodel may have started at a different version, so we check
					// against the readmodel's AggregateVersion rather than a shared checkpoint.
					if (cs.AggregateVersion > readmodel.AggregateVersion)
					{
						readmodel.ProcessChangeset(cs);
					}
				}
			}
			return Task.FromResult(true);
		}

		/// <inheritdoc/>
		public Task OnStartAsync(long indexOrPosition)
		{
			return Task.CompletedTask;
		}

		/// <inheritdoc/>
		public Task StoppedAsync(long indexOrPosition)
		{
			return Task.CompletedTask;
		}
	}
}
