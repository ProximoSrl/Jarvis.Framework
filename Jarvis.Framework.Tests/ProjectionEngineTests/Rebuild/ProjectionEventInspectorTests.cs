using Jarvis.Framework.Kernel.Engine;
using Jarvis.Framework.Kernel.Events;
using Jarvis.Framework.Kernel.Support;
using Jarvis.Framework.Shared.Events;
using Jarvis.Framework.Tests.EngineTests;
using NUnit.Framework;
using System;
using System.Reflection;
using System.Threading.Tasks;

namespace Jarvis.Framework.Tests.ProjectionEngineTests.Rebuild
{
    [TestFixture]
    public class ProjectionEventInspectorTests
    {
        #region Special projections used for test

        private class TestProjectionWithoutInterface
        {
            public void On(SampleAggregateCreated _)
            {
                // Method intentionally left empty.
            }
        }

        private class TestProjectionCatchAll
        {
            public void On(DomainEvent _)
            {
                // Method intentionally left empty.
            }
        }

        private class TestProjectionBaseEvent
        {
            public void On(SampleAggregateBaseEvent _)
            {
                // Method intentionally left empty.
            }
        }

        private class TestProjectionBase
        {
            public void On(SampleAggregateCreated _)
            {
                // Method intentionally left empty.
            }
        }

        private class TestProjectionInherited : TestProjectionBase
        {
            public void On(SampleAggregateTouched _)
            {
                // Method intentionally left empty.
            }
        }

        private class TestProjectionTouched
        {
            public void On(SampleAggregateTouched _)
            {
                // Method intentionally left empty.
            }
        }

        [ProjectionInfo("ProjectionWithEventUpcasted")]
        public class ProjectionWithEventUpcasted : AbstractProjection,
            IEventHandler<SampleAggregateCreated>
        {
            public override Task DropAsync()
            {
                return Task.CompletedTask;
            }

            public override Task SetUpAsync()
            {
                return Task.CompletedTask;
            }

            public Task On(SampleAggregateCreated _)
            {
                return Task.CompletedTask;
            }

            /// <summary>
            /// This is the important part, we use an event that was upcasted, this means
            /// that we need to filter for all old version of the event.
            /// </summary>
            /// <returns></returns>
            public Task On(SampleAggregateUpcasted _)
            {
                return Task.CompletedTask;
            }
        }

        #endregion

        [Test]
        public void verify_can_scan_all_domain_events()
        {
            ProjectionEventInspector sut = new ProjectionEventInspector();
            sut.AddAssembly(Assembly.GetExecutingAssembly());
            Assert.That(sut.TotalEventCount, Is.GreaterThan(0));
        }

        [Test]
        public void verify_get_basic_event_handled()
        {
            ProjectionEventInspector sut = new ProjectionEventInspector();
            sut.AddAssembly(Assembly.GetExecutingAssembly());
            sut.InspectProjectionForEvents(typeof(Projection));
            Assert.That(sut.EventHandled, Is.EquivalentTo(new[] { typeof(SampleAggregateCreated) }));
        }

        [Test]
        public void verify_get_each_call_return_correct_number_of_event()
        {
            ProjectionEventInspector sut = new ProjectionEventInspector();
            sut.AddAssembly(Assembly.GetExecutingAssembly());
            var result = sut.InspectProjectionForEvents(typeof(Projection));
            Assert.That(result, Is.EquivalentTo(new[] { typeof(SampleAggregateCreated) }));

            result = sut.InspectProjectionForEvents(typeof(TestProjectionTouched));
            Assert.That(result, Is.EquivalentTo(new[] { typeof(SampleAggregateTouched) }));

            //Combine the two projection events togheter.
            Assert.That(sut.EventHandled, Is.EquivalentTo(new[] { typeof(SampleAggregateCreated), typeof(SampleAggregateTouched) }));
        }

        [Test]
        public void verify_get_poco_event_handled()
        {
            ProjectionEventInspector sut = new ProjectionEventInspector();
            sut.AddAssembly(Assembly.GetExecutingAssembly());
            sut.InspectProjectionForEvents(typeof(ProjectionWithPoco));
            Assert.That(sut.EventHandled, Is.EquivalentTo(new[] { typeof(SampleAggregateCreated), typeof(PocoPayloadObject) }));
        }

        [Test]
        public void verify_get_basic_event_handled_even_if_no_interface_is_declared()
        {
            ProjectionEventInspector sut = new ProjectionEventInspector();
            sut.AddAssembly(Assembly.GetExecutingAssembly());
            sut.InspectProjectionForEvents(typeof(TestProjectionWithoutInterface));
            Assert.That(sut.EventHandled, Is.EquivalentTo(new[] { typeof(SampleAggregateCreated) }));
        }

        [Test]
        public void verify_inherited_event_handling()
        {
            ProjectionEventInspector sut = new ProjectionEventInspector();
            sut.AddAssembly(Assembly.GetExecutingAssembly());
            sut.InspectProjectionForEvents(typeof(TestProjectionBaseEvent));
            Assert.That(sut.EventHandled, Is.EquivalentTo(new[] { typeof(SampleAggregateBaseEvent), typeof(SampleAggregateDerived1), typeof(SampleAggregateDerived2) }));
        }

        [Test]
        public void verify_event_handled_by_base_projection()
        {
            ProjectionEventInspector sut = new ProjectionEventInspector();
            sut.AddAssembly(Assembly.GetExecutingAssembly());
            sut.InspectProjectionForEvents(typeof(TestProjectionInherited));
            Assert.That(sut.EventHandled, Is.EquivalentTo(new[] { typeof(SampleAggregateCreated), typeof(SampleAggregateTouched) }));
        }

        [Test]
        public void verify_catch_all_projection()
        {
            ProjectionEventInspector sut = new ProjectionEventInspector();
            sut.AddAssembly(Assembly.GetExecutingAssembly());
            sut.InspectProjectionForEvents(typeof(TestProjectionCatchAll));
            Assert.That(sut.EventHandled, Has.Count.EqualTo(sut.TotalEventCount));
        }

        [Test]
        public void Verify_upcasted_events()
        {
            StaticUpcaster.Clear();
            StaticUpcaster.RegisterUpcaster(new SampleAggregateUpcasted_v1.SampleAggregateUpcasted_v1Upcaster());
            StaticUpcaster.RegisterUpcaster(new SampleAggregateUpcasted_v2.SampleAggregateUpcasted_v2Upcaster());

            ProjectionEventInspector sut = new ProjectionEventInspector();
            sut.AddAssembly(Assembly.GetExecutingAssembly());
            sut.InspectProjectionForEvents(typeof(ProjectionWithEventUpcasted));
            Assert.That(sut.EventHandled, Is.EquivalentTo(new Type[]
            {
                typeof(SampleAggregateCreated),
                typeof(SampleAggregateUpcasted),
                typeof(SampleAggregateUpcasted_v1),
                typeof(SampleAggregateUpcasted_v2),
            }));
        }

        [Test]
        public void verify_reset()
        {
            ProjectionEventInspector sut = new ProjectionEventInspector();
            sut.AddAssembly(Assembly.GetExecutingAssembly());
            sut.InspectProjectionForEvents(typeof(TestProjectionCatchAll));
            Assert.That(sut.EventHandled, Has.Count.EqualTo(sut.TotalEventCount));
            sut.ResetHandledEvents();
            sut.InspectProjectionForEvents(typeof(TestProjectionInherited));
            Assert.That(sut.EventHandled, Is.EquivalentTo(new[] { typeof(SampleAggregateCreated), typeof(SampleAggregateTouched) }));
        }
    }
}
