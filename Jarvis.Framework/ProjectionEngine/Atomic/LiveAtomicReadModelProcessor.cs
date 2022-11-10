using Jarvis.Framework.Kernel.ProjectionEngine.Client;
using Jarvis.Framework.Shared.Helpers;
using Jarvis.Framework.Shared.ReadModel.Atomic;
using NStore.Core.Persistence;
using System;
using System.Threading.Tasks;

namespace Jarvis.Framework.Kernel.ProjectionEngine.Atomic
{
    public class LiveAtomicReadModelProcessor : ILiveAtomicReadModelProcessor
    {
        private readonly ICommitEnhancer _commitEnhancer;
        private readonly IPersistence _persistence;
        private readonly IAtomicReadModelFactory _atomicReadModelFactory;

        public LiveAtomicReadModelProcessor(
            IAtomicReadModelFactory atomicReadModelFactory,
            ICommitEnhancer commitEnhancer,
            IPersistence persistence)
        {
            _commitEnhancer = commitEnhancer;
            _persistence = persistence;
            _atomicReadModelFactory = atomicReadModelFactory;
        }

        public async Task<TModel> ProcessAsync<TModel>(String id, Int64 versionUpTo)
            where TModel : IAtomicReadModel
        {
            //Stop condition is version greater than the version we want to apply
            var readmodel = _atomicReadModelFactory.Create<TModel>(id);
            var subscription = new AtomicReadModelSubscription<TModel>(_commitEnhancer, readmodel, cs => cs.AggregateVersion > versionUpTo);
            await _persistence.ReadForwardAsync(id, 0, subscription).ConfigureAwait(false);
            return readmodel.AggregateVersion == 0 ? default : readmodel;
        }

        public async Task<TModel> ProcessAsyncUntilChunkPosition<TModel>(string id, long positionUpTo) where TModel : IAtomicReadModel
        {
            var readmodel = _atomicReadModelFactory.Create<TModel>(id);
            var subscription = new AtomicReadModelSubscription<TModel>(_commitEnhancer, readmodel, cs => cs.GetChunkPosition() > positionUpTo);
            await _persistence.ReadForwardAsync(id, 0, subscription, Int64.MaxValue).ConfigureAwait(false);
            return readmodel.AggregateVersion == 0 ? default : readmodel;
        }

        public Task CatchupAsync<TModel>(TModel readModel) where TModel : IAtomicReadModel
        {
            var subscription = new AtomicReadModelSubscription<TModel>(_commitEnhancer, readModel, cs => false);
            return _persistence.ReadForwardAsync(readModel.Id, readModel.AggregateVersion + 1, subscription, Int64.MaxValue);
        }

        public async Task<TModel> ProcessAsyncUntilUtcTimestamp<TModel>(string id, DateTime dateTimeUpTo) where TModel : IAtomicReadModel
        {
            var readmodel = _atomicReadModelFactory.Create<TModel>(id);
            var subscription = new AtomicReadModelSubscription<TModel>(_commitEnhancer, readmodel, cs => cs.GetTimestamp() > dateTimeUpTo);
            await _persistence.ReadForwardAsync(id, 0, subscription, Int64.MaxValue).ConfigureAwait(false);
            return readmodel.AggregateVersion == 0 ? default : readmodel;
        }
    }
}
