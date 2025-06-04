using Jarvis.Framework.Kernel.ProjectionEngine.Client;
using Jarvis.Framework.Shared.Helpers;
using Jarvis.Framework.Shared.ReadModel.Atomic;
using NStore.Core.Persistence;
using NStore.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static Jarvis.Framework.Shared.ReadModel.Atomic.AtomicReadmodelMultiAggregateSubscription;

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
		public async Task<TModel> ProcessAsync<TModel>(String id, Int64 versionUpTo, CancellationToken cancellationToken = default)
			where TModel : IAtomicReadModel
		{
			//Stop condition is version greater than the version we want to apply
			var readmodel = _atomicReadModelFactory.Create<TModel>(id);
			var subscription = new AtomicReadModelSubscription<TModel>(_commitEnhancer, readmodel, cs => cs.AggregateVersion > versionUpTo);
			await _persistence.ReadForwardAsync(id, 0, subscription, Int64.MaxValue, Int32.MaxValue, cancellationToken).ConfigureAwait(false);
			return readmodel.AggregateVersion == 0 ? default : readmodel;
		}

		/// <inheritdoc/>
		public async Task<TModel> ProcessUntilChunkPositionAsync<TModel>(string id, long positionUpTo, CancellationToken cancellationToken = default) where TModel : IAtomicReadModel
		{
			var readmodel = _atomicReadModelFactory.Create<TModel>(id);
			var subscription = new AtomicReadModelSubscription<TModel>(_commitEnhancer, readmodel, cs => cs.GetChunkPosition() > positionUpTo);
			await _persistence.ReadForwardAsync(id, 0, subscription, Int64.MaxValue, Int32.MaxValue, cancellationToken).ConfigureAwait(false);
			return readmodel.AggregateVersion == 0 ? default : readmodel;
		}

		/// <inheritdoc/>
		public Task CatchupAsync<TModel>(TModel readModel, CancellationToken cancellationToken = default) where TModel : IAtomicReadModel
		{
			var subscription = new AtomicReadModelSubscription<TModel>(_commitEnhancer, readModel, cs => false);
			return _persistence.ReadForwardAsync(readModel.Id, readModel.AggregateVersion + 1, subscription, Int64.MaxValue, Int32.MaxValue, cancellationToken);
		}

		/// <inheritdoc/>
		public async Task<TModel> ProcessUntilUtcTimestampAsync<TModel>(string id, DateTime dateTimeUpTo, CancellationToken cancellationToken = default) where TModel : IAtomicReadModel
		{
			var readmodel = _atomicReadModelFactory.Create<TModel>(id);
			var subscription = new AtomicReadModelSubscription<TModel>(_commitEnhancer, readmodel, cs => cs.GetTimestamp() > dateTimeUpTo);
			await _persistence.ReadForwardAsync(id, 0, subscription, Int64.MaxValue, Int32.MaxValue, cancellationToken).ConfigureAwait(false);
			return readmodel.AggregateVersion == 0 ? default : readmodel;
		}

		public async Task<MultiStreamProcessorResult> ProcessAsync(
            IReadOnlyCollection<MultiStreamProcessRequest> request,
            long checkpointUpToIncluded,
            CancellationToken cancellation = default)
		{
			if (request is null)
			{
				throw new ArgumentNullException(nameof(request));
			}

			//Need to prepare readmodel
			var rmDictionary = request.ToDictionary(
				r => r.AggregateId,
				r => ReadModelProjectionInstruction.SimpleProjection(r.ReadmodelTypes.Select(t => _atomicReadModelFactory.Create(t, r.AggregateId)).ToList()));

			var subscription = new AtomicReadmodelMultiAggregateSubscription(_commitEnhancer, rmDictionary, cs => cs.GetChunkPosition() > checkpointUpToIncluded);
			var idList = request.Select(r => r.AggregateId).ToList();
			await _persistence.ReadForwardMultiplePartitionsAsync(idList, 0, subscription, checkpointUpToIncluded, cancellation).ConfigureAwait(false);
			return new MultiStreamProcessorResult(rmDictionary.Values.Select(v => v.Readmodels));
		}

		public async Task<MultiStreamProcessorResult> ProcessAsync(
            IReadOnlyCollection<MultiStreamProcessRequest> request, DateTime dateTimeUpTo, CancellationToken cancellation = default)
		{
			if (request is null)
			{
				throw new ArgumentNullException(nameof(request));
			}

            //Need to prepare readmodel
            var rmDictionary = request.ToDictionary(
                r => r.AggregateId,
                r => ReadModelProjectionInstruction.SimpleProjection(r.ReadmodelTypes.Select(t => _atomicReadModelFactory.Create(t, r.AggregateId)).ToList()));

            var subscription = new AtomicReadmodelMultiAggregateSubscription(_commitEnhancer, rmDictionary, cs => cs.GetTimestamp() > dateTimeUpTo);
			var idList = request.Select(r => r.AggregateId).ToList();
			await _persistence.ReadForwardMultiplePartitionsAsync(idList, 0, subscription, Int32.MaxValue, cancellation).ConfigureAwait(false);
			return new MultiStreamProcessorResult(rmDictionary.Values.Select(v => v.Readmodels));
		}

		/// <inheritdoc/>
		public async Task<MultipleReadmodelProjectionResult> ProcessAsync(IEnumerable<Type> types, string id, long versionUpTo, CancellationToken cancellation = default)
		{
			//Stop condition is version greater than the version we want to apply
			List<IAtomicReadModel> readModels = types.Select(rmt => _atomicReadModelFactory.Create(rmt, id)).ToList();
			var subscription = new MultipleAtomicReadModelSubscription(_commitEnhancer, readModels, cs => cs.AggregateVersion > versionUpTo);
			await _persistence.ReadForwardAsync(id, 0, subscription, long.MaxValue, int.MaxValue, cancellation).ConfigureAwait(false);
			return new MultipleReadmodelProjectionResult(readModels);
		}

        /// <inheritdoc/>
        public async Task<MultiStreamProcessorResult> ProcessAsync(IReadOnlyCollection<CheckpointMultiStreamProcessRequest> request, CancellationToken cancellation = default)
        {
            if (request is null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            //Need to prepare readmodels
            var rmDictionary = request.ToDictionary(
                r => r.AggregateId,
                r => ReadModelProjectionInstruction.CheckpointProjection(r.ReadmodelTypes.Select(t => _atomicReadModelFactory.Create(t, r.AggregateId)).ToList(), r.CheckpointLimit));

            //Stop reading at max checkpoint
            var maxCheckpoint = request.Max(r => r.CheckpointLimit);

            var subscription = new AtomicReadmodelMultiAggregateSubscription(_commitEnhancer, rmDictionary, cs => cs.GetChunkPosition() > maxCheckpoint);

            var idList = request.Select(r => r.AggregateId).ToList();
            await _persistence.ReadForwardMultiplePartitionsAsync(idList, 0, subscription, Int32.MaxValue, cancellation).ConfigureAwait(false);
            return new MultiStreamProcessorResult(rmDictionary.Values.Select(v => v.Readmodels));
        }

        /// <inheritdoc/>
        public async Task<MultipleReadmodelProjectionResult> ProcessUntilChunkPositionAsync(IEnumerable<Type> types, string id, long positionUpTo, CancellationToken cancellationToken = default)
		{
			List<IAtomicReadModel> readModels = types.Select(rmt => _atomicReadModelFactory.Create(rmt, id)).ToList();
			var subscription = new MultipleAtomicReadModelSubscription(_commitEnhancer, readModels, cs => cs.GetChunkPosition() > positionUpTo);
			await _persistence.ReadForwardAsync(id, 0, subscription, Int64.MaxValue, Int32.MaxValue, cancellationToken).ConfigureAwait(false);
			return new MultipleReadmodelProjectionResult(readModels);
		}

		/// <inheritdoc/>
		public async Task<MultipleReadmodelProjectionResult> ProcessUntilUtcTimestampAsync<TModel>(IEnumerable<Type> types, string id, DateTime dateTimeUpTo, CancellationToken cancellationToken = default)
		{
			List<IAtomicReadModel> readModels = types.Select(rmt => _atomicReadModelFactory.Create(rmt, id)).ToList();
			var subscription = new MultipleAtomicReadModelSubscription(_commitEnhancer, readModels, cs => cs.GetTimestamp() > dateTimeUpTo);
			await _persistence.ReadForwardAsync(id, 0, subscription, Int64.MaxValue, Int32.MaxValue, cancellationToken).ConfigureAwait(false);
			return new MultipleReadmodelProjectionResult(readModels);
		}

        /// <inheritdoc/>
        public async Task<TModel> ProcessUntilAsync<TModel>(
            string id,
            TModel readModel,
            Func<Changeset, TModel, CancellationToken, Task<bool>> changesetConditionEvaluator,
            Func<TModel, CancellationToken, Task<bool>> conditionEvaluator,
            CancellationToken cancellationToken = default) where TModel : IAtomicReadModel
        {
            if (id is null)
            {
                throw new ArgumentNullException(nameof(id));
            }

            if (changesetConditionEvaluator == null && conditionEvaluator == null)
            {
                throw new ArgumentException(nameof(changesetConditionEvaluator), "At least one condition evaluator must be specified.");
            }

            var startReading = readModel?.AggregateVersion + 1 ?? 0;
            readModel ??= _atomicReadModelFactory.Create<TModel>(id);

            var subscription = new AsyncAtomicReadModelSubscription<TModel>(_commitEnhancer, readModel, changesetConditionEvaluator, conditionEvaluator);

            await _persistence.ReadForwardAsync(id, startReading, subscription, Int64.MaxValue, Int32.MaxValue, cancellationToken).ConfigureAwait(false);
            return readModel.AggregateVersion == 0 ? default : readModel;
        }
    }
}
