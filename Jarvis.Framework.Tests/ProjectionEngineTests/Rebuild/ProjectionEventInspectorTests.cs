using Jarvis.Framework.Kernel.Support;
using Jarvis.Framework.Shared.Events;
using Jarvis.Framework.Tests.EngineTests;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Jarvis.Framework.Tests.ProjectionEngineTests.Rebuild
{
    [TestFixture]
    public class ProjectionEventInspectorTests
    {
        private class TestProjectionWithoutInterface
        {
            public void On(SampleAggregateCreated e)
            {
               
            }
        }

        private class TestProjectionCatchAll
        {
            public void On(DomainEvent e)
            {

            }
        }

        private class TestProjectionBaseEvent
        {
            public void On(SampleAggregateBaseEvent e)
            {

            }
        }

        private class TestProjectionBase
        {
            public void On(SampleAggregateCreated e)
            {

            }
        }

        private class TestProjectionInherited : TestProjectionBase
        {
            public void On(SampleAggregateTouched e)
            {

            }
        }

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
