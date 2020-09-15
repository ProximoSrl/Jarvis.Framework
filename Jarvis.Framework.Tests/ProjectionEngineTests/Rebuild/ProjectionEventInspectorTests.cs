using Jarvis.Framework.Tests.EngineTests;

namespace Jarvis.Framework.Tests.ProjectionEngineTests.Rebuild
{
    [TestFixture]
    public class ProjectionEventInspectorTests
    {
#pragma warning disable S1144 // Unused private types or members should be removed
#pragma warning disable RCS1163 // Unused parameter.
        private class TestProjectionWithoutInterface
        {
            public void On(SampleAggregateCreated e)
            {
                // Method intentionally left empty.
            }
        }

        private class TestProjectionCatchAll
        {
            public void On(DomainEvent e)
            {
                // Method intentionally left empty.
            }
        }

        private class TestProjectionBaseEvent
        {
            public void On(SampleAggregateBaseEvent e)
            {
                // Method intentionally left empty.
            }
        }

        private class TestProjectionBase
        {
            public void On(SampleAggregateCreated e)
            {
                // Method intentionally left empty.
            }
        }

        private class TestProjectionInherited : TestProjectionBase
        {
            public void On(SampleAggregateTouched e)
            {
                // Method intentionally left empty.
            }
        }

        private class TestProjectionTouched
        {
            public void On(SampleAggregateTouched e)
            {
                // Method intentionally left empty.
            }
        }

#pragma warning restore RCS1163 // Unused parameter.
#pragma warning restore S1144 // Unused private types or members should be remove

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
