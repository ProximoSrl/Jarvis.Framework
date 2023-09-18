using Jarvis.Framework.Kernel.Engine;
using NStore.Core.Processing;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Jarvis.Framework.Tests.EngineTests.AggregateTests
{
    [TestFixture(false)]
    [TestFixture(true)]
    public class AggregateStateEventPayloadProcessorTests
    {
        private IPayloadProcessor _sut;

        public AggregateStateEventPayloadProcessorTests(bool useFastProcessor)
        {
            if (useFastProcessor)
            {
                _sut = AggregateStateFastEventPayloadProcessor.Instance;
            }
            else
            {
                _sut = AggregateStateEventPayloadProcessor.Instance;
            }
        }

        [OneTimeSetUp]
        public void TestFixtureSetUp()
        {
            _sut = AggregateStateEventPayloadProcessor.Instance;
        }

        [Test]
        public void Verify_applying_state_events()
        {
            var aggregateState = new AggregateTestSampleAggregate1State();
            _sut.Process(aggregateState, new AggregateTestSampleAggregate1Touched());
            Assert.That(aggregateState.TouchCount, Is.EqualTo(1));
        }

        [Test]
        public void Verify_ignore_not_handled_events()
        {
            var aggregateState = new AggregateTestSampleAggregate1State();
            _sut.Process(aggregateState, new object());
            Assert.That(aggregateState.TouchCount, Is.EqualTo(0));
        }

        [Test]
        public void Timing_tests()
        {
            int eventCount = 100000;
            var events = new List<object>(eventCount);
            var aggregateState = new AggregateTestSampleAggregate1State();
            for (int i = 0; i < eventCount; i++)
            {
                events.Add(new AggregateTestSampleAggregate1Touched());
            }
            var sw = Stopwatch.StartNew();
            for (int i = 0;i < events.Count; i++)
            {
                _sut.Process(aggregateState, events[i]);
            }
            sw.Stop();
            Console.WriteLine("Elapsed: {0}ms", sw.ElapsedMilliseconds);
            Assert.That(aggregateState.TouchCount, Is.EqualTo(eventCount));
        }
    }
}
