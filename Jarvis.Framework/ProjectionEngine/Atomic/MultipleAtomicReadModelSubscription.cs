using Jarvis.Framework.Kernel.ProjectionEngine.Client;
using NStore.Core.Persistence;
using NStore.Domain;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Jarvis.Framework.Shared.ReadModel.Atomic
{
	/// <summary>
	/// Subscription for NStore to apply events to a series of atomic readmodels.
	/// Useful when complex aggregate requires more than one single #45
	/// </summary>
	public sealed class MultipleAtomicReadModelSubscription : ISubscription
    {
        private readonly ICommitEnhancer _commitEnhancer;
		private readonly IReadOnlyCollection<IAtomicReadModel> _readmodels;
		private readonly Func<Changeset, Boolean> _stopCondition;

		/// <summary>
		/// Project an atomic readmodel.
		/// </summary>
		/// <param name="commitEnhancer"></param>
		/// <param name="readmodels"></param>
		/// <param name="stopCondition">This condition will be evaluated BEFORE the changeset will be processed
		/// by the readmodel, if the return value is true, the iteration will stop.</param>
		public MultipleAtomicReadModelSubscription(
            ICommitEnhancer commitEnhancer,
            IReadOnlyCollection<IAtomicReadModel> readmodels,
            Func<Changeset, Boolean> stopCondition)
        {
            _commitEnhancer = commitEnhancer;
			_readmodels = readmodels;
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
                foreach (var readModel in _readmodels)
                {
					readModel.ProcessChangeset(cs);
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
