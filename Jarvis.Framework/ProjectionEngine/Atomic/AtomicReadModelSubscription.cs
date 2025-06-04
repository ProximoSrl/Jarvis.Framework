using Jarvis.Framework.Kernel.ProjectionEngine.Client;
using NStore.Core.Persistence;
using NStore.Domain;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Jarvis.Framework.Shared.ReadModel.Atomic
{
	/// <summary>
	/// Subscription for NStore to simplify applying events to an 
	/// atomic readmodel.
	/// </summary>
	/// <typeparam name="TModel"></typeparam>
	public sealed class AtomicReadModelSubscription<TModel> : ISubscription
		where TModel : IAtomicReadModel
	{
		private readonly ICommitEnhancer _commitEnhancer;
		private readonly Func<Changeset, Boolean> _stopCondition;
		private readonly TModel _readModel;

		/// <summary>
		/// Project an atomic readmodel.
		/// </summary>
		/// <param name="commitEnhancer"></param>
		/// <param name="readModel"></param>
		/// <param name="stopCondition">This condition will be evaluated BEFORE the changeset will be processed
		/// by the readmodel, if the return value is true, the iteration will stop.</param>
		public AtomicReadModelSubscription(
			ICommitEnhancer commitEnhancer,
			TModel readModel,
			Func<Changeset, Boolean> stopCondition)
		{
			_commitEnhancer = commitEnhancer;
			_stopCondition = stopCondition;
			_readModel = readModel;
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
				_readModel.ProcessChangeset(cs);
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

    public sealed class AsyncAtomicReadModelSubscription<TModel> : ISubscription
        where TModel : IAtomicReadModel
    {
        private readonly ICommitEnhancer _commitEnhancer;
        private readonly TModel _readModel;
        private readonly Func<Changeset, TModel, CancellationToken, Task<bool>> _stopConditionOnChangeset;
        private readonly Func<TModel, CancellationToken, Task<bool>> _stopConditionOnReadModel;

        /// <summary>
        /// Project an atomic readmodel.
        /// </summary>
        public AsyncAtomicReadModelSubscription(
            ICommitEnhancer commitEnhancer,
            TModel readModel,
            Func<Changeset, TModel, CancellationToken, Task< Boolean>> stopConditionOnChangeset,
            Func<TModel, CancellationToken, Task<Boolean>> stopConditionOnReadModel
            )
        {
            _commitEnhancer = commitEnhancer;
            _readModel = readModel;

            //not both the condition can be null, at least one of them must be specified.
            if (stopConditionOnChangeset == null && stopConditionOnReadModel == null)
            {
                throw new ArgumentNullException("At least one of the stop conditions must be specified.");
            }

            _stopConditionOnChangeset = stopConditionOnChangeset ?? ((_, _, _) => Task.FromResult(false));
            _stopConditionOnReadModel = stopConditionOnReadModel ?? ((_, _) => Task.FromResult(false));
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
        public async Task<bool> OnNextAsync(IChunk chunk)
        {
            _commitEnhancer.Enhance(chunk);
            if (chunk.Payload is Changeset cs)
            {
                var shouldStopOnChangeset = await _stopConditionOnChangeset(cs, _readModel, CancellationToken.None);
                if (shouldStopOnChangeset) return false;

                _readModel.ProcessChangeset(cs);
                var shouldStopOnReadModel = await _stopConditionOnReadModel(_readModel, CancellationToken.None);
                if (shouldStopOnReadModel) return false;
            }
            return true;
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
