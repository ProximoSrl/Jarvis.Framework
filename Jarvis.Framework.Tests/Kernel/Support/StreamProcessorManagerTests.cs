namespace Jarvis.Framework.Tests.Kernel.Support
{
    [TestFixture]
    public class StreamProcessorManagerTests
    {
        private InMemoryPersistence persistence;
        private StreamProcessorManager sut;

        [SetUp]
        public void SetUp()
        {
            persistence = new InMemoryPersistence();
            sut = new StreamProcessorManager(persistence);
        }

        [Test]
        public async Task Verify_basic_apply_of_NON_domain_events_payload()
        {
            await persistence.AppendAsync("partitionId", new PocoObject(2)).ConfigureAwait(false);
            var result = await sut.ProcessAsync<SimpleProjection>("partitionId", Int32.MaxValue).ConfigureAwait(false);
            Assert.That(result.EvtCount, Is.EqualTo(1));
            Assert.That(result.Value, Is.EqualTo(2));
        }

        [Test]
        public async Task Verify_basic_apply_of_changeset()
        {
            Changeset cs = new Changeset(1, new[] { new PocoObject(2), new PocoObject(3) });
            await persistence.AppendAsync("partitionId", cs).ConfigureAwait(false);
            var result = await sut.ProcessAsync<SimpleProjection>("partitionId", Int32.MaxValue).ConfigureAwait(false);
            Assert.That(result.EvtCount, Is.EqualTo(2));
            Assert.That(result.Value, Is.EqualTo(5));
        }

        #region Aux Classes

        public class PocoObject
        {
            public PocoObject(int value)
            {
                Value = value;
            }

            public Int32 Value { get; set; }
        }

        public class SimpleProjection
        {
            public Int32 EvtCount { get; set; }

            public Int32 Value { get; set; }

            protected void On(PocoObject obj)
            {
                EvtCount++;
                Value += obj.Value;
            }
        }

        #endregion
    }
}
