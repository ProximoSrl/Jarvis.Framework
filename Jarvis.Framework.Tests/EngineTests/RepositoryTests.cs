﻿using System;
using System.Configuration;
using System.Diagnostics;
using Castle.Core.Logging;
using CommonDomain.Core;
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

namespace Jarvis.Framework.Tests.EngineTests
{
    [TestFixture]
    public class RepositoryTests : ITenantAccessor
    {
        private RepositoryEx _repository;
        private MongoDatabase _db;
        private IStoreEvents _eventStore;
        private IdentityManager _identityConverter;
        private AggregateFactory _aggregateFactory = new AggregateFactory(null);

        [SetUp]
        public void SetUp()
        {
            Current = new Tenant(new TenantATestSettings());

            var connectionString = ConfigurationManager.ConnectionStrings["eventstore"].ConnectionString;
            var url = new MongoUrl(connectionString);
            var client = new MongoClient(url);
            _db = client.GetServer().GetDatabase(url.DatabaseName);
            _db.Drop();

            _identityConverter = new IdentityManager(new InMemoryCounterService());
            _identityConverter.RegisterIdentitiesFromAssembly(GetType().Assembly);
            var loggerFactory = Substitute.For<ILoggerFactory>();
            loggerFactory.Create(Arg.Any<Type>()).Returns(NullLogger.Instance);
            _eventStore = new EventStoreFactory(loggerFactory).BuildEventStore(connectionString);
            _repository = CreateRepository();
        }

        private RepositoryEx CreateRepository()
        {
            return new RepositoryEx(
                _eventStore,
                _aggregateFactory, new ConflictDetector(), _identityConverter
            );
        }

        [TearDown]
        public void TearDown()
        {
            _repository.Dispose();
        }

        [Test, Explicit]
        public void profile_snapshot_opt_out()
        {
            var sampleAggregateId = new SampleAggregateId(1);
            var aggregate = TestAggregateFactory.Create<SampleAggregate, SampleAggregate.State>(new SampleAggregate.State(), sampleAggregateId);
            aggregate.Create();

            _repository.Save(aggregate, Guid.NewGuid(), null);

            int max = 20;
            for (int t = 1; t < max; t++)
            {
                aggregate.Touch();
                _repository.Save(aggregate, Guid.NewGuid(), null);

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
                    _identityConverter))
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

            var aggregate = TestAggregateFactory.Create<SampleAggregate, SampleAggregate.State>(new SampleAggregate.State(), sampleAggregateId);
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
         
        [Test]
        public void repository_raise_domain_exception_when_domain_rule_failed()
        {
            var sampleAggregateId = new SampleAggregateId(1);

            var state = new SampleAggregate.State() { TouchCount = 3 };
            IDomainRule<SampleAggregate.State> rule = new TouchCountRule(3);

            var aggregate = TestAggregateFactory
                .Create<SampleAggregate, SampleAggregate.State>(state, sampleAggregateId)
                .AssignRulesForTest(new[] { rule});
            aggregate.Create();
            aggregate.Touch(); //this is good, but state does not validate external rule.
            try
            {
                _repository.Save(aggregate, new Guid("135E4E5F-3D65-43AC-9D8D-8A8B0EFF8501"), null);
                Assert.Fail("We expect a DomainException");
            }
            catch (DomainException ex)
            {
                Assert.That(ex.AggregateId, Is.EqualTo(sampleAggregateId.AsString()));
                Assert.That(ex.Message, Is.StringContaining("Rule violated, cannot touch more than 3 times"));
            }
            catch (AssertionException ex)
            {
                throw;
            }
            catch (Exception ex)
            {
                Assert.Fail("We expect an exception of type DomainException but we catched " + ex.GetType().Name);
            }
        }

        [Test]
        public void repository_command_handler_should_set_context()
        {
            var cmd = new TouchSampleAggregate(new SampleAggregateId(1));
            cmd.SetContextData("key", "value");
            var handler = new TouchSampleAggregateHandler
            {
                Repository = _repository,
                AggregateFactory = _aggregateFactory
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
            var aggregate = TestAggregateFactory.Create<SampleAggregate, SampleAggregate.State>(new SampleAggregate.State(), sampleAggregateId);
            aggregate.Create();
            _repository.Save(aggregate, new Guid("135E4E5F-3D65-43AC-9D8D-8A8B0EFF8501"), null);

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

    }

    internal class TouchCountRule : IDomainRule<SampleAggregate.State>
    {
        private Int32 _maxTouchCount;

        public TouchCountRule(Int32 maxTouchCount)
        {
            _maxTouchCount = maxTouchCount;
        }

        public void Validate(IIdentity id, SampleAggregate.State state)
        {
            if (state.TouchCount > _maxTouchCount)
                throw new DomainException(id, String.Format("Rule violated, cannot touch more than {0} times", _maxTouchCount));
        }

    }
}
