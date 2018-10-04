using Castle.Facilities.TypedFactory;
using Castle.MicroKernel.Registration;
using Castle.Windsor;
using Jarvis.Framework.Kernel.ProjectionEngine;
using Jarvis.Framework.Kernel.ProjectionEngine.Atomic;
using Jarvis.Framework.Kernel.ProjectionEngine.Client;
using Jarvis.Framework.Kernel.Support;
using Jarvis.Framework.Shared.Events;
using Jarvis.Framework.Shared.Helpers;
using Jarvis.Framework.Shared.IdentitySupport;
using Jarvis.Framework.Shared.ReadModel;
using Jarvis.Framework.Shared.ReadModel.Atomic;
using Jarvis.Framework.Tests.EngineTests;
using Jarvis.Framework.Tests.ProjectionsTests.Atomic.Support;
using MongoDB.Driver;
using NStore.Core.InMemory;
using NStore.Core.Persistence;
using NStore.Domain;
using NUnit.Framework;
using System;
using System.Configuration;
using System.Threading.Tasks;

namespace Jarvis.Framework.Tests.ProjectionsTests.Atomic
{
    public abstract class AtomicCollectionWrapperTestsBase
    {
        protected AtomicMongoCollectionWrapper<SimpleTestAtomicReadModel> _sut;
        protected IMongoDatabase _db;
        protected InMemoryPersistence _persistence;
        protected IMongoCollection<SimpleTestAtomicReadModel> _collection;
        protected IdentityManager _identityManager;

        protected void Init()
        {
            var url = new MongoUrl(ConfigurationManager.ConnectionStrings["readmodel"].ConnectionString);
            var client = new MongoClient(url);
            _db = client.GetDatabase(url.DatabaseName);
            _collection = _db.GetCollection<SimpleTestAtomicReadModel>(
                CollectionNames.GetCollectionName<SimpleTestAtomicReadModel>());
            _persistence = new InMemoryPersistence();
            _db.Drop();

            _identityManager = new IdentityManager(new CounterService(_db));
        }

        protected void InitSingleTest()
        {
            _lastCommit = 1;
            _lastPosition = 0;
            _aggregateVersion = 1;
            _aggregateIdSeed++;
            SimpleTestAtomicReadModel.FakeSignature = 1;

            GenerateSut();
        }

        protected void GenerateSut()
        {
            _sut = new AtomicMongoCollectionWrapper<SimpleTestAtomicReadModel>(
               _db,
               new AtomicReadModelFactory(),
               new LiveAtomicReadModelProcessor(new AtomicReadModelFactory(), new CommitEnhancer(), _persistence));
        }

        protected Int64 _lastCommit;
        protected Int32 _lastPosition;

        protected Int64 _aggregateIdSeed = 1;
        protected Int32 _aggregateVersion = 1;

        protected Changeset GenerateCreatedEvent(Boolean inSameCommitAsPrevious)
        {
            SampleAggregateCreated evt = new SampleAggregateCreated();
            return ProcessEvent(evt, inSameCommitAsPrevious);
        }

        protected Changeset GenerateTouchedEvent(Boolean inSameCommitAsPrevious)
        {
            SampleAggregateTouched evt = new SampleAggregateTouched();
            return ProcessEvent(evt, inSameCommitAsPrevious);
        }

        protected Changeset ProcessEvent(DomainEvent evt, bool inSameCommitAsPrevious)
        {
            Int64 commitId = inSameCommitAsPrevious ? _lastCommit : ++_lastCommit;

            evt.SetPropertyValue(d => d.AggregateId, new SampleAggregateId(_aggregateIdSeed));
            evt.SetPropertyValue(d => d.CheckpointToken, commitId);
            Changeset cs = new Changeset(_aggregateVersion++, evt);
            _persistence.AppendAsync(evt.AggregateId, cs).Wait();
            return cs;
        }
    }
}

