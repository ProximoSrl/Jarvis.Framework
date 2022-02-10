using Jarvis.Framework.Kernel.ProjectionEngine.Client;
using Jarvis.Framework.Shared.Events;
using Jarvis.Framework.Shared.Messages;
using Jarvis.Framework.TestHelpers;
using Jarvis.Framework.Tests.EngineTests;
using NStore.Domain;
using NUnit.Framework;
using System;
using System.Linq;

namespace Jarvis.Framework.Tests.Kernel.Support
{
    [TestFixture]
    public class CommitEnhancerTests
    {
        private CommitEnhancer _sut;
        private Changeset _payload;
        private readonly SampleAggregateId _sampleAggregateId = new SampleAggregateId(1);
        private readonly DateTime _date1 = new DateTime(2010, 01, 01);
        private readonly DateTime _date2 = new DateTime(2010, 01, 02);

        [SetUp]
        public void SetUp()
        {
            _sut = new CommitEnhancer();
        }

        [Test]
        public void Can_copy_all_headers()
        {
            var chunk = CreateTestChunk(new Object(), CreateAnEvent(), new Object());
            _payload.Add("Foo", "Bar");
            _sut.Enhance(chunk);

            var evt = chunk.DomainEvents[0];

            //no specific data, standard commit data should be used.
            Assert.That(evt.Context.Single().Key, Is.EqualTo("Foo"));
            Assert.That(evt.Context.Single().Value, Is.EqualTo("Bar"));
        }

        [Test]
        public void Special_header_copy()
        {
            var chunk = CreateTestChunk(new Object(), CreateAnEvent(), new Object());
            _payload.Add(ChangesetCommonHeaders.Timestamp, _date2);
            _payload.Add(MessagesConstants.UserId, "User_2");

            _sut.Enhance(chunk);

            var evt = chunk.DomainEvents[0];

            //no specific data, standard commit data should be used.
            Assert.That(evt.IssuedBy, Is.EqualTo("User_2"));
            Assert.That(evt.CommitStamp, Is.EqualTo(_date2));
        }

        [Test]
        public void Verify_override_of_users()
        {
            var chunk = CreateTestChunk(new Object(), CreateAnEvent());
            _payload.Add(MessagesConstants.UserId, "User_1");
            _payload.Add(MessagesConstants.OnBehalfOf, "User_2");

            _sut.Enhance(chunk);

            var evt = chunk.DomainEvents[0];

            //no specific data, standard commit data should be used.
            Assert.That(evt.IssuedBy, Is.EqualTo("User_2"));
            Assert.That(evt.Context[MessagesConstants.UserId], Is.EqualTo("User_1"));
            Assert.That(evt.Context[MessagesConstants.OnBehalfOf], Is.EqualTo("User_2"));
        }

        [Test]
        public void Timestamp_command_override()
        {
            var chunk = CreateTestChunk(new Object(), CreateAnEvent());
            _payload.Add(ChangesetCommonHeaders.Timestamp, _date2);
            _payload.Add(MessagesConstants.OverrideCommitTimestamp, _date1);

            _sut.Enhance(chunk);

            var evt = chunk.DomainEvents[0];

            //no specific data, standard commit data should be used.
            Assert.That(evt.CommitStamp, Is.EqualTo(_date1));
        }

        [Test]
        public void Timestamp_command_override_error_set()
        {
            var chunk = CreateTestChunk(new Object(), CreateAnEvent());
            _payload.Add(ChangesetCommonHeaders.Timestamp, _date2);

            // set an invalida value for timestamp override
            _payload.Add(MessagesConstants.OverrideCommitTimestamp, new object());

            _sut.Enhance(chunk);

            var evt = chunk.DomainEvents[0];

            //no specific data, standard commit data should be used.
            Assert.That(evt.CommitStamp, Is.EqualTo(_date2));
        }

        //[Test]
        //public void Resiliency_for_something_that_is_not_domain_Event()
        //{
        //    var chunk = CreateTestChunk(new Object(), CreateAnEvent("domain\\gm", _date2), new Object());
        //    var evt = chunk.DomainEvents[0];
        //    _payload.Headers.Add(DraftCommittedHeader.DraftCommittedHeaderKey, new DraftCommittedHeader()
        //    {
        //        DraftId = _draftId,
        //        SecurityTokens = new string[] { "sec" },
        //        EventsData = new Dictionary<string, DraftCommittedHeader.EventData>()
        //        {
        //            //Pay attention, we tell the system that the original event was raised by different user and different date
        //            [evt.MessageId.ToString()] = new DraftCommittedHeader.EventData("domain\\gmoriginal", _date1)
        //        }
        //    });
        //    _sut.Enhance(chunk);
        //    //no specific data, standard commit data should be used.
        //    Assert.That(evt.CommitStamp, Is.EqualTo(_date1));
        //    Assert.That(evt.IssuedBy, Is.EqualTo("domain\\gmoriginal"));
        //}

        private TestChunk CreateTestChunk(params Object[] events)
        {
            _payload = new Changeset(1, events);
            return new TestChunk(1, _sampleAggregateId, 1, _payload, Guid.NewGuid());
        }

        private SampleAggregateCreated CreateAnEvent()
        {
            var evt = new SampleAggregateCreated();
            evt.AssignIdForTest(_sampleAggregateId);
            return evt;
        }
    }
}
