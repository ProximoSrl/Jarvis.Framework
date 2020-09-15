using Jarvis.Framework.Tests.EngineTests;
using Jarvis.Framework.Tests.ProjectionsTests.Atomic.Support;

namespace Jarvis.Framework.Tests.ProjectionsTests.Atomic
{
    public abstract class AtomicCollectionWrapperTestsBase
    {
        protected AtomicMongoCollectionWrapper<SimpleTestAtomicReadModel> _sut;
        protected IMongoDatabase _db;
        protected InMemoryPersistence _persistence;
        protected IMongoCollection<SimpleTestAtomicReadModel> _collection;
        protected IMongoCollection<BsonDocument> _mongoBsonCollection;
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
            _mongoBsonCollection = _db.GetCollection<BsonDocument>(CollectionNames.GetCollectionName<SimpleTestAtomicReadModel>());
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

        protected Changeset GenerateInvalidatedEvent(Boolean inSameCommitAsPrevious)
        {
            SampleAggregateInvalidated evt = new SampleAggregateInvalidated();
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
            Changeset cs = new Changeset(_aggregateVersion++, new Object[] { evt });
            _persistence.AppendAsync(evt.AggregateId, cs).Wait();
            return cs;
        }
    }
}

