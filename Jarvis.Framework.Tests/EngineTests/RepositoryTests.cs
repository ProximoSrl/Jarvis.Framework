using System;
using System.Configuration;
using Castle.Core.Logging;
using CommonDomain.Core;
using Jarvis.Framework.Kernel.Engine;
using Jarvis.Framework.Kernel.MultitenantSupport;
using Jarvis.Framework.Kernel.Store;
using Jarvis.Framework.Shared.IdentitySupport;
using Jarvis.Framework.Shared.MultitenantSupport;
using Jarvis.Framework.TestHelpers;
using Jarvis.NEventStoreEx.CommonDomainEx.Core;
using Jarvis.NEventStoreEx.CommonDomainEx.Persistence.EventStore;
using MongoDB.Driver;
using NEventStore;
using NSubstitute;
using NUnit.Framework;

namespace Jarvis.Framework.Tests.EngineTests
{
    [TestFixture]
    public class RepositoryTests : ITenantAccessor
    {
        private RepositoryEx _repository;
        private MongoDatabase _db;
        private IStoreEvents _eventStore;

        [SetUp]
        public void SetUp()
        {
            Current = new Tenant(new TenantATestSettings());

            var connectionString = ConfigurationManager.ConnectionStrings["eventstore"].ConnectionString;
            var url = new MongoUrl(connectionString);
            var client = new MongoClient(url);
            _db = client.GetServer().GetDatabase(url.DatabaseName);
            _db.Drop();

            var identityConverter = new IdentityManager(new InMemoryCounterService());
            identityConverter.RegisterIdentitiesFromAssembly(GetType().Assembly);
            var loggerFactory = Substitute.For<ILoggerFactory>();
            loggerFactory.Create(Arg.Any<Type>()).Returns(NullLogger.Instance);
            _eventStore = new EventStoreFactory(loggerFactory).BuildEventStore(connectionString);

            _repository = new RepositoryEx(
                _eventStore,
                new AggregateFactory(null), new ConflictDetector(), identityConverter
                );
        }

        [Test]
        public void can_save_with_aggregate_identity()
        {
            var sampleAggregateId = new SampleAggregateId(1);
            var aggregate = TestAggregateFactory.Create<SampleAggregate, SampleAggregate.State>(
                new SampleAggregate.State(), 
                sampleAggregateId
            );
            aggregate.Create();
            _repository.Save(aggregate, new Guid("135E4E5F-3D65-43AC-9D8D-8A8B0EFF8501"), null);

            var stream = _eventStore.OpenStream("Jarvis", sampleAggregateId, int.MinValue, int.MaxValue);

            Assert.IsNotNull(stream);
            Assert.AreEqual(1, stream.CommittedEvents.Count);
        }

        [Test]
        public void can_save_and_load()
        {
            var sampleAggregateId = new SampleAggregateId(1);

            var aggregate = TestAggregateFactory.Create < SampleAggregate, SampleAggregate.State>(new SampleAggregate.State(), sampleAggregateId);
            aggregate.Create();
            _repository.Save(aggregate, new Guid("135E4E5F-3D65-43AC-9D8D-8A8B0EFF8501"), null);

            var loaded = _repository.GetById<SampleAggregate>(sampleAggregateId);

            Assert.IsTrue(loaded.HasBeenCreated);
        }

        [Test]
        public void raise_exception_if_invariants_are_not_satisfied()
        {
            var sampleAggregateId = new SampleAggregateId(1);

            var aggregate = TestAggregateFactory.Create<SampleAggregate, SampleAggregate.State>(new SampleAggregate.State(), sampleAggregateId);
            aggregate.Create();
            aggregate.InvalidateState();
            try
            {
                _repository.Save(aggregate, new Guid("135E4E5F-3D65-43AC-9D8D-8A8B0EFF8501"), null);
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

        public ITenant Current { get; private set; }
        public ITenant GetTenant(TenantId id)
        {
            throw new NotImplementedException();
        }


        public ITenant[] Tenants
        {
            get { throw new NotImplementedException(); }
        }
    }
}
