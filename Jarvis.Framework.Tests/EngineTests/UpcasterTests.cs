using Jarvis.Framework.Kernel.Engine;
using Jarvis.Framework.Shared.Events;
using Jarvis.Framework.Shared.Helpers;
using Jarvis.Framework.Shared.IdentitySupport;
using Jarvis.Framework.Shared.Store;
using Jarvis.Framework.TestHelpers;
using Newtonsoft.Json;
using NStore.Domain;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Jarvis.Framework.Tests.EngineTests
{
    [TestFixture]
    public class ChangesetUpcasterTest
    {
        [VersionInfo(Version = 1, Name = "ReallyOldEvent")]
        private class ReallyOldEvent : DomainEvent
        {
            public ReallyOldEvent(Int32 property)
            {
                Property = property;
            }

            public Int32 Property { get; set; }

            public class Upcaster : BaseUpcaster<ReallyOldEvent, UpcastedEvent>
            {
                protected override UpcastedEvent OnUpcast(ReallyOldEvent eventToUpcast)
                {
                    return new UpcastedEvent("Property " + eventToUpcast.Property.ToString());
                }
            }
        }

        private class UpcastedEvent : DomainEvent
        {
            public UpcastedEvent(string property)
            {
                Property = property;
            }

            public String Property { get; set; }

            public class Upcaster : BaseUpcaster<UpcastedEvent, NewEvent>
            {
                protected override NewEvent OnUpcast(UpcastedEvent eventToUpcast)
                {
                    return new NewEvent(eventToUpcast.Property);
                }
            }
        }

        private class NewEvent : DomainEvent
        {
            public NewEvent(string renamedProperty)
            {
                RenamedProperty = renamedProperty;
            }

            public String RenamedProperty { get; set; }
        }

        [Serializable]
        private class UpcastClassTestId : EventStoreIdentity
        {
            public UpcastClassTestId(long id) : base(id)
            {
            }

            [JsonConstructor]
            public UpcastClassTestId(string id) : base(id)
            {
            }
        }

        [Test]
        public void Upcast_of_single_event()
        {
            var evt = new UpcastedEvent("Hello world");
            var upcaster = new UpcastedEvent.Upcaster();
            var evtUpcasted = upcaster.Upcast(evt);
            Assert.That(evtUpcasted.RenamedProperty, Is.EqualTo("Hello world"));
        }

        [Test]
        public void Can_detect_upcast_chain()
        {
            StaticUpcaster.Clear();
            StaticUpcaster.RegisterUpcaster(new UpcastedEvent.Upcaster());
            StaticUpcaster.RegisterUpcaster(new ReallyOldEvent.Upcaster());
            var chainOfUpcastedEvents = StaticUpcaster.FindUpcastedEvents(typeof(NewEvent));

            //Assert: we need to verify that the chain of upcasted event is correct
            Assert.That(chainOfUpcastedEvents, Is.EquivalentTo(new Type[] { typeof(ReallyOldEvent), typeof(UpcastedEvent) }));
        }

        [Test]
        public void Can_detect_intermediate_upcast_chain()
        {
            StaticUpcaster.Clear();
            StaticUpcaster.RegisterUpcaster(new UpcastedEvent.Upcaster());
            StaticUpcaster.RegisterUpcaster(new ReallyOldEvent.Upcaster());
            var chainOfUpcastedEvents = StaticUpcaster.FindUpcastedEvents(typeof(UpcastedEvent));

            //Assert: we need to verify that the chain of upcasted event is correct
            Assert.That(chainOfUpcastedEvents, Is.EquivalentTo(new Type[] { typeof(ReallyOldEvent) }));
        }

        [Test]
        public void Upcast_of_single_event_copy_all_properties()
        {
            var evt = new UpcastedEvent("Hello world")
                .AssignIdForTest(new UpcastClassTestId(1));
            evt.SetPropertyValue(e => e.CommitId, Guid.NewGuid().ToString());
            evt.SetPropertyValue(e => e.Version, 42L);
            evt.SetPropertyValue(e => e.CheckpointToken, 42L);
            evt.SetPropertyValue(e => e.Context, new Dictionary<String, Object>()
            {
                ["truth"] = 42,
                [ChangesetCommonHeaders.Timestamp] = DateTime.UtcNow,
            });

            var upcaster = new UpcastedEvent.Upcaster();
            var evtUpcasted = upcaster.Upcast(evt);
            Assert.That(evtUpcasted.CommitId, Is.EqualTo(evt.CommitId));
            Assert.That(evtUpcasted.CommitStamp, Is.EqualTo(evt.CommitStamp));
            Assert.That(evtUpcasted.Version, Is.EqualTo(evt.Version));
            Assert.That(evtUpcasted.CheckpointToken, Is.EqualTo(evt.CheckpointToken));
            Assert.That(evtUpcasted.Context["truth"], Is.EqualTo(42));
            Assert.That(evtUpcasted.AggregateId, Is.EqualTo(new UpcastClassTestId(1)));
        }

        [Test]
        public void Upcast_of_single_event_copy_event_sequence()
        {
            var evt = new UpcastedEvent("Hello world")
                .AssignIdForTest(new UpcastClassTestId(1));
            evt.SetPropertyValue(e => e.CommitId, Guid.NewGuid().ToString());
            evt.SetPropertyValue(e => e.Version, 42L);
            evt.SetPropertyValue(e => e.CheckpointToken, 42L);
            evt.SetPropertyValue(e => e.EventPosition, 3);

             var upcaster = new UpcastedEvent.Upcaster();
            var evtUpcasted = upcaster.Upcast(evt);
            Assert.That(evtUpcasted.EventPosition, Is.EqualTo(3));
        }

        [Test]
        public void Cannot_Register_two_distinct_upcasters()
        {
            StaticUpcaster.Clear();
            StaticUpcaster.RegisterUpcaster(new UpcastedEvent.Upcaster());
            Assert.Throws<ArgumentException>(() => StaticUpcaster.RegisterUpcaster(new UpcastedEvent.Upcaster()));
        }

        [Test]
        public void Simple_Upcast_of_changeset()
        {
            StaticUpcaster.Clear();
            StaticUpcaster.RegisterUpcaster(new UpcastedEvent.Upcaster());
            var cs = new Changeset(1, new object[] { new UpcastedEvent("Hello world") });
            var upcasted = StaticUpcaster.UpcastChangeset(cs);
            Assert.That(object.ReferenceEquals(upcasted, cs));
            Assert.That(upcasted, Is.InstanceOf<Changeset>());
            var upcastedCs = (Changeset)upcasted;
            Assert.That(upcastedCs.Events.Single(), Is.InstanceOf<NewEvent>());

            var upcastedEvent = (NewEvent)upcastedCs.Events.Single();
            Assert.That(upcastedEvent.RenamedProperty, Is.EqualTo("Hello world"));
        }

        [Test]
        public void Simple_Upcast_of_single_event()
        {
            StaticUpcaster.Clear();
            StaticUpcaster.RegisterUpcaster(new UpcastedEvent.Upcaster());
            var evt = new UpcastedEvent("Hello world");
            var upcasted = StaticUpcaster.UpcastEvent(evt);

            Assert.That(upcasted, Is.InstanceOf<NewEvent>());

            var upcastedEvent = (NewEvent) upcasted;
            Assert.That(upcastedEvent.RenamedProperty, Is.EqualTo("Hello world"));
        }

        /// <summary>
        /// We have ReallyOldEvent upcasted to UpcastedEvent that in turn should be
        /// upcasted to NewEvent
        /// </summary>
        [Test]
        public void Chain_of_upcast()
        {
            StaticUpcaster.Clear();
            StaticUpcaster.RegisterUpcaster(new UpcastedEvent.Upcaster());
            StaticUpcaster.RegisterUpcaster(new ReallyOldEvent.Upcaster());

            var evt = new ReallyOldEvent(42);
            var upcasted = StaticUpcaster.UpcastEvent(evt);

            Assert.That(upcasted, Is.InstanceOf<NewEvent>());

            var upcastedEvent = (NewEvent)upcasted;
            Assert.That(upcastedEvent.RenamedProperty, Is.EqualTo("Property 42"));
        }

        [Test]
        public void Upcast_should_be_resilient_to_null()
        {
            var upcasted = StaticUpcaster.UpcastChangeset(null);
            NUnit.Framework.Legacy.ClassicAssert.IsNull(upcasted);
        }

        [Test]
        public void Upcast_should_be_resilient_to_non_changeset_object()
        {
            var upcasted = StaticUpcaster.UpcastChangeset("Hello world");
            Assert.That(upcasted, Is.EqualTo("Hello world"));
        }
    }
}
