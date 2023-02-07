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
	/// Subscription for NStore to project multiple identity, multiple
	/// readmodels.
	/// </summary>
	public sealed class AtomicReadmodelMultiAggregateSubscription : ISubscription
	{
		private readonly ICommitEnhancer _commitEnhancer;
		private readonly IDictionary<string, IReadOnlyCollection<IAtomicReadModel>> _projectionInstructions;
		private readonly Func<Changeset, Boolean> _stopCondition;

		/// <summary>
		/// Project an atomic readmodel.
		/// </summary>
		/// <param name="commitEnhancer"></param>
		/// <param name="projectionInstructions">A dictionary where we have aggregate id as a key, and the
		/// list of associated readmodel as value.</param>
		/// <param name="stopCondition">This condition will be evaluated BEFORE the changeset will be processed
		/// by the readmodel, if the return value is true, the iteration will stop.</param>
		public AtomicReadmodelMultiAggregateSubscription(
			ICommitEnhancer commitEnhancer,
			IDictionary<string, IReadOnlyCollection<IAtomicReadModel>> projectionInstructions,
			Func<Changeset, Boolean> stopCondition)
		{
			_commitEnhancer = commitEnhancer;
			_projectionInstructions = projectionInstructions;
			_stopCondition = stopCondition;
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
				if (_projectionInstructions.TryGetValue(cs.GetIdentity().AsString(), out var readmodelList))
				{
					foreach (var rm in readmodelList)
					{
						rm.ProcessChangeset(cs);
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
