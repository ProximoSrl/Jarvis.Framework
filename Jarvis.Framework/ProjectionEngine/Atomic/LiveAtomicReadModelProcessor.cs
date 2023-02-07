using Jarvis.Framework.Kernel.ProjectionEngine.Client;
using Jarvis.Framework.Shared.Helpers;
using Jarvis.Framework.Shared.ReadModel.Atomic;
using NStore.Core.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static MongoDB.Driver.WriteConcern;

namespace Jarvis.Framework.Kernel.ProjectionEngine.Atomic
{
	/// <inheritdoc/>
	public class LiveAtomicReadModelProcessor : ILiveAtomicReadModelProcessor, ILiveAtomicMultistreamReadModelProcessor, ILiveAtomicReadModelProcessorEnhanced
	{
		private readonly ICommitEnhancer _commitEnhancer;
		private readonly IPersistence _persistence;
		private readonly IAtomicReadModelFactory _atomicReadModelFactory;

		/// <summary>
		/// Constructor
		/// </summary>
		/// <param name="atomicReadModelFactory"></param>
		/// <param name="commitEnhancer"></param>
		/// <param name="persistence"></param>
		public LiveAtomicReadModelProcessor(
			IAtomicReadModelFactory atomicReadModelFactory,
			ICommitEnhancer commitEnhancer,
			IPersistence persistence)
		{
			_commitEnhancer = commitEnhancer;
			_persistence = persistence;
			_atomicReadModelFactory = atomicReadModelFactory;
		}

		/// <inheritdoc/>
		public async Task<TModel> ProcessAsync<TModel>(String id, Int64 versionUpTo)
			where TModel : IAtomicReadModel
		{
			//Stop condition is version greater than the version we want to apply
			var readmodel = _atomicReadModelFactory.Create<TModel>(id);
			var subscription = new AtomicReadModelSubscription<TModel>(_commitEnhancer, readmodel, cs => cs.AggregateVersion > versionUpTo);
			await _persistence.ReadForwardAsync(id, 0, subscription).ConfigureAwait(false);
			return readmodel.AggregateVersion == 0 ? default : readmodel;
		}

		/// <inheritdoc/>
		public async Task<TModel> ProcessAsyncUntilChunkPosition<TModel>(string id, long positionUpTo) where TModel : IAtomicReadModel
		{
			var readmodel = _atomicReadModelFactory.Create<TModel>(id);
			var subscription = new AtomicReadModelSubscription<TModel>(_commitEnhancer, readmodel, cs => cs.GetChunkPosition() > positionUpTo);
			await _persistence.ReadForwardAsync(id, 0, subscription, Int64.MaxValue).ConfigureAwait(false);
			return readmodel.AggregateVersion == 0 ? default : readmodel;
		}

		/// <inheritdoc/>
		public Task CatchupAsync<TModel>(TModel readModel) where TModel : IAtomicReadModel
		{
			var subscription = new AtomicReadModelSubscription<TModel>(_commitEnhancer, readModel, cs => false);
			return _persistence.ReadForwardAsync(readModel.Id, readModel.AggregateVersion + 1, subscription, Int64.MaxValue);
		}

		/// <inheritdoc/>
		public async Task<TModel> ProcessAsyncUntilUtcTimestamp<TModel>(string id, DateTime dateTimeUpTo) where TModel : IAtomicReadModel
		{
			var readmodel = _atomicReadModelFactory.Create<TModel>(id);
			var subscription = new AtomicReadModelSubscription<TModel>(_commitEnhancer, readmodel, cs => cs.GetTimestamp() > dateTimeUpTo);
			await _persistence.ReadForwardAsync(id, 0, subscription, Int64.MaxValue).ConfigureAwait(false);
			return readmodel.AggregateVersion == 0 ? default : readmodel;
		}

		public async Task<MultiStreamProcessorResult> ProcessAsync(IReadOnlyCollection<MultiStreamProcessRequest> request, long checkpointUpToIncluded)
		{
			if (request is null)
			{
				throw new ArgumentNullException(nameof(request));
			}

			//Need to prepare readmodel
			IDictionary<string, IReadOnlyCollection<IAtomicReadModel>> rmDictionary = request.ToDictionary(
				r => r.AggregateId,
				r => r.ReadmodelTypes.Select(t => _atomicReadModelFactory.Create(t, r.AggregateId)).ToList() as IReadOnlyCollection<IAtomicReadModel>);

			var subscription = new AtomicReadmodelMultiAggregateSubscription(_commitEnhancer, rmDictionary, cs => cs.GetChunkPosition() > checkpointUpToIncluded);
			var idList = request.Select(r => r.AggregateId).ToList();
			await _persistence.ReadForwardMultiplePartitionsAsync(idList, 0, subscription, checkpointUpToIncluded).ConfigureAwait(false);
			return new MultiStreamProcessorResult(rmDictionary.Values);
		}

		public async Task<MultiStreamProcessorResult> ProcessAsync(IReadOnlyCollection<MultiStreamProcessRequest> request, DateTime dateTimeUpTo)
		{
			if (request is null)
			{
				throw new ArgumentNullException(nameof(request));
			}

			//Need to prepare readmodel
			IDictionary<string, IReadOnlyCollection<IAtomicReadModel>> rmDictionary = request.ToDictionary(
				r => r.AggregateId,
				r => r.ReadmodelTypes.Select(t => _atomicReadModelFactory.Create(t, r.AggregateId)).ToList() as IReadOnlyCollection<IAtomicReadModel>);

			var subscription = new AtomicReadmodelMultiAggregateSubscription(_commitEnhancer, rmDictionary, cs => cs.GetTimestamp() > dateTimeUpTo);
			var idList = request.Select(r => r.AggregateId).ToList();
			await _persistence.ReadForwardMultiplePartitionsAsync(idList, 0, subscription, Int32.MaxValue).ConfigureAwait(false);
			return new MultiStreamProcessorResult(rmDictionary.Values);
		}

		/// <inheritdoc/>
		public async Task<MultipleReadmodelProjectionResult> ProcessAsync(IEnumerable<Type> types, string id, long versionUpTo)
		{
			//Stop condition is version greater than the version we want to apply
			List<IAtomicReadModel> readModels = types.Select(rmt => _atomicReadModelFactory.Create(rmt, id)).ToList();
			var subscription = new MultipleAtomicReadModelSubscription(_commitEnhancer, readModels, cs => cs.AggregateVersion > versionUpTo);
			await _persistence.ReadForwardAsync(id, 0, subscription).ConfigureAwait(false);
			return new MultipleReadmodelProjectionResult(readModels);
		}

		/// <inheritdoc/>
		public async Task<MultipleReadmodelProjectionResult> ProcessAsyncUntilChunkPosition(IEnumerable<Type> types, string id, long positionUpTo)
		{
			List<IAtomicReadModel> readModels = types.Select(rmt => _atomicReadModelFactory.Create(rmt, id)).ToList();
			var subscription = new MultipleAtomicReadModelSubscription(_commitEnhancer, readModels, cs => cs.GetChunkPosition() > positionUpTo);
			await _persistence.ReadForwardAsync(id, 0, subscription, Int64.MaxValue).ConfigureAwait(false);
			return new MultipleReadmodelProjectionResult(readModels);
		}

		/// <inheritdoc/>
		public async Task<MultipleReadmodelProjectionResult> ProcessAsyncUntilUtcTimestamp<TModel>(IEnumerable<Type> types, string id, DateTime dateTimeUpTo)
		{
			List<IAtomicReadModel> readModels = types.Select(rmt => _atomicReadModelFactory.Create(rmt, id)).ToList();
			var subscription = new MultipleAtomicReadModelSubscription(_commitEnhancer, readModels, cs => cs.GetTimestamp() > dateTimeUpTo);
			await _persistence.ReadForwardAsync(id, 0, subscription, Int64.MaxValue).ConfigureAwait(false);
			return new MultipleReadmodelProjectionResult(readModels);
		}
	}
}
