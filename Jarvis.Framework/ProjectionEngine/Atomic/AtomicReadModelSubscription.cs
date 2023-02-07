using Jarvis.Framework.Kernel.ProjectionEngine.Client;
using NStore.Core.Persistence;
using NStore.Domain;
using System;
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
}
