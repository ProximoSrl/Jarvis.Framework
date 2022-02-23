using Jarvis.Framework.Kernel.ProjectionEngine.Client;
using Jarvis.Framework.Tests.EngineTests;
using NStore.Core.InMemory;
using NStore.Domain;
using NUnit.Framework;
using System.Threading.Tasks;
using NStore.Core.Persistence;
using Jarvis.Framework.Shared.Events;
using Jarvis.Framework.Tests.Support;
using NStore.Core.Streams;
using Jarvis.Framework.Shared.Helpers;

namespace Jarvis.Framework.Tests.ProjectionEngineTests
{
    [TestFixture]
    public class CommitEnhancerTests
    {
        [Test]
        public async Task Enanche_deep_copy_context_dictionary()
        {
            var id = new SampleAggregateId(1);
            var evt1 = GenerateEvent<SampleAggregateCreated>(id);
            var evt2 = GenerateEvent<SampleAggregateTouched>(id);
            var evt3 = GenerateEvent<SampleAggregateTouched>(id);
            Changeset cs = new Changeset(1, new object[] { evt1, evt2, evt3 });
            cs.Headers.Add("myHeader", "myValue");
            var persistence = new InMemoryPersistence();
            var streamsFactory = new StreamsFactory(persistence);
            var stream = streamsFactory.Open(id);

            var chunk = await stream.AppendAsync(cs).ConfigureAwait(false);

            var sut = new CommitEnhancer();

            sut.Enhance(chunk);

            var expectedCs = chunk.Payload as Changeset;
            //if override context entries of second event
            (expectedCs.Events[1] as DomainEvent).Context["myHeader"] = "otherValue";
            // the context of the first event should remain the same
            Assert.That((expectedCs.Events[0] as DomainEvent).Context["myHeader"], Is.EqualTo("myValue"));
        }

        private static T GenerateEvent<T>(SampleAggregateId id) where T : DomainEvent, new()
        {
            T evt1 = new T();
            evt1.SetPropertyValue(d => d.AggregateId, id);
            return evt1;
        }
    }
}
