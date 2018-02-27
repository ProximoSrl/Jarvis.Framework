using Jarvis.Framework.Kernel.ProjectionEngine.Client;
using Jarvis.Framework.Shared.Events;
using Jarvis.Framework.Shared.IdentitySupport;
using Jarvis.Framework.Tests.EngineTests;
using NStore.Domain;
using NStore.Persistence.Mongo;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jarvis.Framework.Tests.ProjectionEngineTests
{
    [TestFixture]
    public class CommitEnhancerTests
    {
        private CommitEnhancer _sut;

        [SetUp]
        public void SetUp()
        {
            var cs = new InMemoryCounterService();
            var identityManager = new IdentityManager(cs);
            _sut = new CommitEnhancer(identityManager);
        }

        [Test]
        public void Verify_correct_setting_of_event_sequence()
        {
            var chunk = new MongoChunk();
            Changeset cs = new Changeset(1,
                new SampleAggregateCreated(),
                new SampleAggregateTouched());

            chunk.Init(10, new SampleAggregateId(2), 1, cs , "TEST");
            _sut.Enhance(chunk);
            Assert.That(((DomainEvent)cs.Events[0]).EventPosition, Is.EqualTo(1));
            Assert.That(((DomainEvent)cs.Events[1]).EventPosition, Is.EqualTo(2));
        }

        [Test]
        public void Verify_correct_set_is_last_event_of_commit()
        {
            var chunk = new MongoChunk();
            Changeset cs = new Changeset(1,
                new SampleAggregateCreated(),
                new SampleAggregateTouched());

            chunk.Init(10, new SampleAggregateId(2), 1, cs, "TEST");
            _sut.Enhance(chunk);
            Assert.That(((DomainEvent)cs.Events[0]).EventPosition, Is.EqualTo(1));
            Assert.That(((DomainEvent)cs.Events[1]).EventPosition, Is.EqualTo(2));

            Assert.That(((DomainEvent)cs.Events[0]).IsLastEventOfCommit, Is.False);
            Assert.That(((DomainEvent)cs.Events[1]).IsLastEventOfCommit, Is.True);
        }

        [Test]
        public void Verify_resiliency_on_empty_commit()
        {
            var chunk = new MongoChunk();
            Changeset cs = new Changeset(1);

            chunk.Init(10, new SampleAggregateId(2), 1, cs, "TEST");
            //Call and fail if some exception is raised.
            _sut.Enhance(chunk);
        }
    }
}
