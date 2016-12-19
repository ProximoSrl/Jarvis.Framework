using System;
using System.Linq;
using System.Security.Principal;
using System.Threading;
using Jarvis.Framework.Kernel.Engine;
using Jarvis.Framework.Kernel.Store;
using Jarvis.Framework.Shared.Events;
using Jarvis.NEventStoreEx.CommonDomainEx;
using NUnit.Core;
using NUnit.Framework;
using Castle.Windsor;

// ReSharper disable InconsistentNaming
namespace Jarvis.Framework.Tests.EngineTests
{
    [TestFixture]
    public class AggregateTests
    {
        AggregateFactory _factory;

        [SetUp]
        public void Setup()
        {
            WindsorContainer container = new WindsorContainer();
            _factory = new AggregateFactory(container.Kernel);
        }

        [Test]
        public void aggregate_should_have_correct_event_router()
        {
            var aggregate = new SampleAggregateForInspection();
            var router = aggregate.GetRouter();
            Assert.IsTrue(router is AggregateRootEventRouter<SampleAggregate.SampleAggregateState>);
        }

        [Test]
        public void hasBeenCreated_on_a_new_aggregate_should_return_false()
        {
            var aggregate = new SampleAggregate();
            Assert.IsFalse(aggregate.HasBeenCreated);
        }

        [Test]
        public void uncommitted_events_not_stored_during_reply()
        {
            var aggregate = (IAggregateEx)new SampleAggregate();
            aggregate.ApplyEvent(new SampleAggregateCreated());
            aggregate.ApplyEvent(new SampleAggregateTouched());

            Assert.That(aggregate.GetUncommittedEvents().Count, Is.EqualTo(0));
        }


        [Test]
        public void verify_validation_of_snapshot()
        {
            SampleAggregate sut = CreateAggregate(new SampleAggregateId(1));
            sut.Touch();
            var memento = ((ISnapshotable)sut).GetSnapshot();

            SampleAggregate sut2 = CreateAggregate(new SampleAggregateId(1));
            Assert.That(((ISnapshotable)sut2).Restore(memento), Is.True);
            Assert.That(sut2.InternalState.TouchCount, Is.EqualTo(1));

            sut2 = CreateAggregate(new SampleAggregateId(1));
            sut2.InternalState.SetSignature("Modified");
            Assert.That(((ISnapshotable)sut2).Restore(memento), Is.False);

        }

        private SampleAggregate CreateAggregate(SampleAggregateId aggregateId)
        {
            return (SampleAggregate)_factory.Build(typeof(SampleAggregate), aggregateId);
        }
    }
}
