using System;
using System.Configuration;
using System.Diagnostics;
using Castle.Core.Logging;
using NEventStore.Domain.Core;
using Jarvis.Framework.Kernel.Commands;
using Jarvis.Framework.Kernel.Engine;
using Jarvis.Framework.Kernel.MultitenantSupport;
using Jarvis.Framework.Kernel.Store;
using Jarvis.Framework.Shared.IdentitySupport;
using Jarvis.Framework.Shared.MultitenantSupport;
using Jarvis.Framework.TestHelpers;
using Jarvis.NEventStoreEx.CommonDomainEx;
using Jarvis.NEventStoreEx.CommonDomainEx.Core;
using Jarvis.NEventStoreEx.CommonDomainEx.Persistence.EventStore;
using MongoDB.Driver;
using NEventStore;
using NSubstitute;
using NUnit.Framework;
using System.Threading.Tasks;
using System.Threading;
using Jarvis.Framework.Shared.Helpers;
using Jarvis.NEventStoreEx.CommonDomainEx.Persistence;
using MongoDB.Bson.Serialization;
using Jarvis.Framework.Shared.IdentitySupport.Serialization;
using Jarvis.NEventStoreEx.Support;
using Jarvis.Framework.Tests.EngineTests;
using Jarvis.NEventStoreEx;
using Castle.Windsor;
using Castle.Facilities.TypedFactory;
using Castle.MicroKernel.Registration;

namespace Jarvis.Framework.Tests.NeventStoreExTests.Persistence
{
    [TestFixture]
    public class RepositoryTests : ITenantAccessor
    {
        private RepositoryEx _sut;
        private IMongoDatabase _db;
        private IStoreEvents _eventStore;
        private IdentityManager _identityConverter;
        private AggregateFactory _aggregateFactory = new AggregateFactory(null);
        private const Int32 NumberOfCommitsBeforeSnapshot = 50;
        private IAggregateCachedRepositoryFactory _aggregateCachedRepositoryFactory;
		private WindsorContainer _container;

        [SetUp]
        public void SetUp()
        {
            Current = new Tenant(new TenantATestSettings());

            var connectionString = ConfigurationManager.ConnectionStrings["eventstore"].ConnectionString;
            var url = new MongoUrl(connectionString);
            var client = new MongoClient(url);
            _db = client.GetDatabase(url.DatabaseName);
            client.DropDatabase(url.DatabaseName);

            _identityConverter = new IdentityManager(new InMemoryCounterService());
            _identityConverter.RegisterIdentitiesFromAssembly(GetType().Assembly);
            MongoFlatIdSerializerHelper.Initialize(_identityConverter);
            var loggerFactory = Substitute.For<ILoggerFactory>();
            loggerFactory.Create(Arg.Any<Type>()).Returns(NullLogger.Instance);
            _eventStore = new EventStoreFactory(loggerFactory).BuildEventStore(connectionString);
            _sut = CreateRepository();

			_container = new WindsorContainer();
			_container.AddFacility<TypedFactoryFacility>();
			_container.Register(Component.For<IRepositoryExFactory>().AsFactory());
			_container.Register(Component
				.For<IRepositoryEx>()
				.UsingFactoryMethod(() => CreateRepository())
				.LifestyleTransient());

			_aggregateCachedRepositoryFactory = new AggregateCachedRepositoryFactory(
				_container.Resolve <IRepositoryExFactory>(),
                false);
        }

        private RepositoryEx CreateRepository()
        {
            var repositoryEx = new RepositoryEx(
                _eventStore,
                _aggregateFactory,
                new ConflictDetector(),
                _identityConverter,
                NSubstitute.Substitute.For<NEventStore.Logging.ILog>()
            );
            repositoryEx.SnapshotManager = Substitute.For<ISnapshotManager>();
            repositoryEx.SnapshotManager.Load("", 0, typeof(SampleAggregate)).ReturnsForAnyArgs<ISnapshot>((ISnapshot)null);
            return repositoryEx;
        }

        [TearDown]
        public void TearDown()
        {
            _sut.Dispose();
        }

        [Test, Explicit]
        public void profile_snapshot_opt_out()
        {
            var sampleAggregateId = new SampleAggregateId(1);
            var aggregate = TestAggregateFactory.Create<SampleAggregate, SampleAggregate.SampleAggregateState>(new SampleAggregate.SampleAggregateState(), sampleAggregateId);
            aggregate.Create();

            _sut.Save(aggregate, Guid.NewGuid(), null);

            int max = 20;
            for (int t = 1; t < max; t++)
            {
                aggregate.Touch();
                _sut.Save(aggregate, Guid.NewGuid(), null);

                if (t == max - 5)
                {
                    var snap = ((ISnapshotable)aggregate).GetSnapshot();
                    _eventStore.Advanced.AddSnapshot(new Snapshot("Jarvis", sampleAggregateId.AsString(), aggregate.Version, snap));
                }
            }

            SnapshotsSettings.OptOut(typeof(SampleAggregate));

            var sw = new Stopwatch();
            sw.Start();
            for (int c = 1; c <= 100; c++)
            {
                using (var repo = new RepositoryEx(
                    _eventStore,
                    _aggregateFactory,
                    new ConflictDetector(),
                    _identityConverter,
                    NSubstitute.Substitute.For<NEventStore.Logging.ILog>()))
                {
                    var loaded = repo.GetById<SampleAggregate>(sampleAggregateId);
                }
            }
            sw.Stop();
            SnapshotsSettings.ClearOptOut();
            Debug.WriteLine("Read time {0} ms", sw.ElapsedMilliseconds);
        }


        [Test]
        public void can_save_with_aggregate_identity()
        {
            var sampleAggregateId = new SampleAggregateId(1);
            var aggregate = TestAggregateFactory.Create<SampleAggregate, SampleAggregate.SampleAggregateState>(
                new SampleAggregate.SampleAggregateState(),
                sampleAggregateId
            );
            aggregate.Create();
            _sut.Save(aggregate, new Guid("135E4E5F-3D65-43AC-9D8D-8A8B0EFF8501"), null);

            var stream = _eventStore.OpenStream("Jarvis", sampleAggregateId, int.MinValue, int.MaxValue);

            Assert.IsNotNull(stream);
            Assert.AreEqual(1, stream.CommittedEvents.Count);
        }

        [Test]
        public void can_reuse_repository_with_same_aggregate()
        {
            var sampleAggregateId = new SampleAggregateId(1);
            var aggregate = TestAggregateFactory.Create<SampleAggregate, SampleAggregate.SampleAggregateState>(
                new SampleAggregate.SampleAggregateState(),
                sampleAggregateId
            );
            aggregate.Create();
            _sut.Save(aggregate, Guid.NewGuid(), null);

            aggregate.Touch();
            _sut.Save(aggregate, Guid.NewGuid(), null);

            var stream = _eventStore.OpenStream("Jarvis", sampleAggregateId, int.MinValue, int.MaxValue);

            Assert.IsNotNull(stream);
            Assert.AreEqual(2, stream.CommittedEvents.Count);
        }

        [Test]
        public void can_reuse_repository_reloading_aggregate()
        {
            var sampleAggregateId = new SampleAggregateId(1);
            var aggregate = TestAggregateFactory.Create<SampleAggregate, SampleAggregate.SampleAggregateState>(
                new SampleAggregate.SampleAggregateState(),
                sampleAggregateId
            );
            aggregate.Create();
            _sut.Save(aggregate, Guid.NewGuid(), null);

            var reloaded = _sut.GetById<SampleAggregate>(sampleAggregateId);

            reloaded.Touch();
            _sut.Save(reloaded, Guid.NewGuid(), null);

            var stream = _eventStore.OpenStream("Jarvis", sampleAggregateId, int.MinValue, int.MaxValue);

            Assert.IsNotNull(stream);
            Assert.AreEqual(2, stream.CommittedEvents.Count);
        }

        [Test]
        public void can_save_and_load()
        {
            var sampleAggregateId = new SampleAggregateId(1);

            var aggregate = TestAggregateFactory.Create<SampleAggregate, SampleAggregate.SampleAggregateState>(new SampleAggregate.SampleAggregateState(), sampleAggregateId);
            aggregate.Create();
            _sut.Save(aggregate, new Guid("135E4E5F-3D65-43AC-9D8D-8A8B0EFF8501"), null);

            var loaded = _sut.GetById<SampleAggregate>(sampleAggregateId);

            Assert.IsTrue(loaded.HasBeenCreated);
        }

        [Test]
        public void raise_exception_if_invariants_are_not_satisfied()
        {
            var sampleAggregateId = new SampleAggregateId(1);

            var aggregate = TestAggregateFactory.Create<SampleAggregate, SampleAggregate.SampleAggregateState>(new SampleAggregate.SampleAggregateState(), sampleAggregateId);
            aggregate.Create();
            aggregate.InvalidateState();
            try
            {
                _sut.Save(aggregate, new Guid("135E4E5F-3D65-43AC-9D8D-8A8B0EFF8501"), null);
                Assert.Fail("We expect an exception");
            }
            catch (InvariantNotSatifiedException ex)
            {
                Assert.That(ex.AggregateId, Is.EqualTo(sampleAggregateId.AsString()));
            }
            catch (Exception ex)
            {
                Assert.Fail("We expect an exception of type InvariantNotSatifiedException but we catched " + ex.GetType().Name);
            }
        }

        [Test]
        public void repository_command_handler_should_set_context()
        {
            var cmd = new TouchSampleAggregate(new SampleAggregateId(1));
            cmd.SetContextData("key", "value");
			IRepositoryExFactory factory = NSubstitute.Substitute.For<IRepositoryExFactory>();
			factory.Create().Returns(_sut);
			var handler = new TouchSampleAggregateHandler
            {
                Repository = _sut,
                AggregateFactory = _aggregateFactory,
                AggregateCachedRepositoryFactory = new AggregateCachedRepositoryFactory(factory)
            };

            handler.Handle(cmd);

            var context = handler.Aggregate.ExposeContext;
            Assert.NotNull(context);
            Assert.AreEqual("TouchSampleAggregate", context["command.name"]);
            Assert.AreEqual("value", context["key"]);
        }

        public ITenant Current { get; private set; }
        public ITenant GetTenant(TenantId id)
        {
            throw new NotImplementedException();
        }


        public ITenant[] Tenants
        {
            get { throw new NotImplementedException(); }
        }

        [Test]
        public void can_serialize_access_to_the_same_entity()
        {
            //create an aggregate.
            var sampleAggregateId = new SampleAggregateId(1);
            var aggregate = TestAggregateFactory.Create<SampleAggregate, SampleAggregate.SampleAggregateState>(new SampleAggregate.SampleAggregateState(), sampleAggregateId);
            aggregate.Create();
            _sut.Save(aggregate, new Guid("135E4E5F-3D65-43AC-9D8D-8A8B0EFF8501"), null);
            NeventStoreExGlobalConfiguration.SetLockThreadSleepCount(100);
            using (var repo1 = CreateRepository())
            using (var repo2 = CreateRepository())
            {
                aggregate = repo1.GetById<SampleAggregate>(sampleAggregateId);
                aggregate.Touch();

                //now create another thread that loads and change the same entity
                var task = Task<Boolean>.Factory.StartNew(() =>
                {
                    var aggregate2 = repo2.GetById<SampleAggregate>(sampleAggregateId);
                    aggregate2.Touch();
                    repo2.Save(aggregate2, Guid.NewGuid(), null);
                    return true;
                });

                Thread.Sleep(100); //Let be sure the other task is started doing something.
                repo1.Save(aggregate, Guid.NewGuid(), null); //should not throw
                Assert.IsTrue(task.Result); //inner should not throw.
            }
        }

        [Test]
        public void verify_saving_snapshot()
        {
            //create an aggregate.
            var sampleAggregateId = new SampleAggregateId(1);
            var aggregate = TestAggregateFactory.Create<SampleAggregate, SampleAggregate.SampleAggregateState>(new SampleAggregate.SampleAggregateState(), sampleAggregateId);
            aggregate.Create();
            for (int i = 0; i < NumberOfCommitsBeforeSnapshot; i++)
            {
				aggregate.Touch();
			}
            var events = _sut.Save(aggregate, new Guid("135E4E5F-3D65-43AC-9D8D-8A8B0EFF8501"), null);
            _sut.SnapshotManager.Received().Snapshot(aggregate, "Jarvis", events);
        }

        [Test]
        public void verify_reloading_with_snapshot()
        {
            //create an aggregate.
            var sampleAggregateId = new SampleAggregateId(1);
            var aggregate = TestAggregateFactory.Create<SampleAggregate, SampleAggregate.SampleAggregateState>(new SampleAggregate.SampleAggregateState(), sampleAggregateId);
            aggregate.Create();
            for (int i = 0; i < NumberOfCommitsBeforeSnapshot; i++)
            {
				aggregate.Touch();
            }
			Assert.That(aggregate.InternalState.TouchCount, Is.EqualTo(50));
			_sut.SnapshotManager = new CachedSnapshotManager(
                new MongoSnapshotPersisterProvider(_db, NullLogger.Instance),
                new NullSnapshotPersistenceStrategy());
            //this will save the snapshot
            _sut.Save(aggregate, new Guid("135E4E5F-3D65-43AC-9D8D-8A8B0EFF8501"), null);

            //now reload the aggregate
            var reloaded = _sut.GetById<SampleAggregate>(sampleAggregateId);
            Assert.That(reloaded.SnapshotRestoreVersion, Is.EqualTo(51));
			Assert.That(reloaded.InternalState.TouchCount, Is.EqualTo(50));
        }

		[Test]
		public void verify_typed_factory_dispose()
		{
			var factory = _container.Resolve<IRepositoryExFactory>();
			var repo = factory.Create();
			Assert.That(repo, Is.Not.Null);
			factory.Release(repo);
			var realRepo = (RepositoryEx)repo;
			Assert.That(realRepo.Disposed, Is.True);
		}


		[Test]
        public void verify_single_cached_aggregate_repository_tolerance_to_domain_exception()
        {
            var id = new SampleAggregateId(1);

            //First call, everything is ok.
            Int32 stateCount;
            SampleAggregate aggregate;

            using (var repo = _aggregateCachedRepositoryFactory.Create<SampleAggregate>(id))
            {
                aggregate = repo.Aggregate;
                aggregate.Create();
                aggregate.Touch();
                repo.Save(Guid.NewGuid(), updateHeaders => { });
                stateCount = aggregate.InternalState.TouchCount;
            }

            try
            {
                //second call, methdo that throws exception
                using (var repo = _aggregateCachedRepositoryFactory.Create<SampleAggregate>(id))
                {
                    Assert.That(Object.ReferenceEquals(repo.Aggregate, aggregate), "Cache is not working");
                    aggregate = repo.Aggregate;
                    aggregate.Create();
                    aggregate.TouchWithThrow(); //this throws
                    repo.Save(Guid.NewGuid(), updateHeaders => { });
                }
            }
            catch (Exception)
            {
                //Ignore the exception 
            }

            //now I want the repository to be fresh with new aggregate
            using (var repo = _aggregateCachedRepositoryFactory.Create<SampleAggregate>(id))
            {
                Assert.That(repo.Aggregate.InternalState.TouchCount, Is.EqualTo(stateCount));
                Assert.That(Object.ReferenceEquals(repo.Aggregate, aggregate), Is.False, "Cache should be invalidated");
            }
        }


    }

}
