using Jarvis.Framework.Shared.Events;
using Jarvis.Framework.Shared.Messages;
using NStore.Domain;
using NUnit.Framework;
using System;
using System.Collections.Generic;

namespace Jarvis.Framework.Tests.SharedTests.Events
{
    [TestFixture]
    public class BasicDomainEventTests
    {
        [Test]
        public void Verify_issued_by()
        {
            var sut = new TestDomainEvent();
            var context = new Dictionary<string, object>();
            context.Add(MessagesConstants.UserId, "User_1234");
            sut.Context = context;

            Assert.That(sut.IssuedBy, Is.EqualTo("User_1234"));
        }

        [Test]
        public void Verify_issued_by_override_by_correct_header()
        {
            var sut = new TestDomainEvent();
            var context = new Dictionary<string, object>();
            context.Add(MessagesConstants.UserId, "User_1234");
            context.Add(MessagesConstants.OnBehalfOf, "User_42");
            sut.Context = context;

            Assert.That(sut.IssuedBy, Is.EqualTo("User_42"));
        }

        [Test]
        public void Verify_issued_by_is_resilient_to_null()
        {
            var sut = new TestDomainEvent();
            sut.Context = null;

            Assert.That(sut.IssuedBy, Is.Null, "Context is null, issued by is not present");
        }

        [Test]
        public void Verify_issued_by_is_resilient_to_missing_value()
        {
            var sut = new TestDomainEvent();
            sut.Context = new Dictionary<string, object>();

            Assert.That(sut.IssuedBy, Is.Null, "Context is null, issued by is not present");
        }

        [Test]
        public void Commit_timestamp_taken_from_base_header()
        {
            var sut = new TestDomainEvent();
            var changeset = new Changeset(1, new[] { sut });
            var ts = new DateTime(2020, 12, 3, 23, 21, 4);
            changeset.Add(ChangesetCommonHeaders.Timestamp, ts);
            sut.Context = changeset.Headers;
            Assert.That(sut.CommitStamp, Is.EqualTo(ts));
        }

        [Test]
        public void Commit_timestamp_can_be_overridden()
        {
            var sut = new TestDomainEvent();
            var changeset = new Changeset(1, new[] { sut });
            var ts = new DateTime(2020, 12, 3, 23, 21, 4);
            var ts2 = ts.AddDays(219);
            changeset.Add(ChangesetCommonHeaders.Timestamp, ts);
            sut.Context = changeset.Headers;
            Assert.That(sut.CommitStamp, Is.EqualTo(ts));

            //Act: add the override header
            var context = new Dictionary<string, object>();
            context.Add(MessagesConstants.OverrideCommitTimestamp, ts2);
            sut.Context = context;

            //Assert: verify the override timestamp
            Assert.That(sut.CommitStamp, Is.EqualTo(ts2));
        }

        [Test]
        public void Commit_timestamp_resilient_to_null()
        {
            var sut = new TestDomainEvent();
            var changeset = new Changeset(1, new[] { sut });

            Assert.That(sut.CommitStamp, Is.EqualTo(DateTime.MinValue));

            // now add a null value.
            changeset.Add(ChangesetCommonHeaders.Timestamp, null);

            Assert.That(sut.CommitStamp, Is.EqualTo(DateTime.MinValue));
        }

        private class TestDomainEvent : DomainEvent { }
    }
}
