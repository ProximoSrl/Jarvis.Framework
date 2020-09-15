using Jarvis.Framework.Kernel.ProjectionEngine.Atomic;
using Jarvis.Framework.Shared.Events;
using Jarvis.Framework.Shared.Helpers;
using Jarvis.Framework.Shared.ReadModel.Atomic;
using Jarvis.Framework.Tests.ProjectionsTests.Atomic.Support;
using NStore.Domain;
using NUnit.Framework;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Jarvis.Framework.Tests.ProjectionsTests.Atomic
{
    [TestFixture]
    public class AtomicReadModelSignatureFixerTests : AtomicProjectionEngineTestBase
    {
        [Test]
        public async Task Verify_basic_fix_for_readmodel()
        {
            //Arrange: Generate some commit and project them 
            Changeset changeset = await GenerateSomeChangesetsAndReturnLatestsChangeset().ConfigureAwait(false);
            var engine = await CreateSutAndStartProjectionEngineAsync().ConfigureAwait(false);
            GetTrackerAndWaitForChangesetToBeProjected("SimpleTestAtomicReadModel");
            await engine.StopAsync().ConfigureAwait(false);

            //Act, start the fixer and change signature
            SimpleTestAtomicReadModel.FakeSignature = 2;
            var sut = GenerateSut();
            sut.AddReadmodelToFix(typeof(SimpleTestAtomicReadModel));
            sut.StartFixing();

            //ok I'm expecting the fix to correct the readmodel
            AssertForReadmodelCondition<SimpleTestAtomicReadModel>(changeset, rm => rm.ReadModelVersion == 2 && rm.TouchCount == 4);
        }

        /// <summary>
        /// Poll readmodel until the readmodel does not satisfy condition or timeout is reached.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="changeset"></param>
        /// <param name="conditionToAssert"></param>
        private void AssertForReadmodelCondition<T>(Changeset changeset, Func<T, Boolean> conditionToAssert)
            where T : class, IAtomicReadModel
        {
            var firstEvent = changeset.Events[0] as DomainEvent;
            DateTime startWait = DateTime.UtcNow;
            var collection = GetCollection<T>();
            while (DateTime.UtcNow.Subtract(startWait).TotalSeconds < 5)
            {
                var record = collection.FindOneById(firstEvent.AggregateId.AsString());
                if (record != null && conditionToAssert(record))
                {
                    return; //Assertion is correct
                }

                Thread.Sleep(100);
            }
            Assert.Fail("Condition not met in the allotted timespan");
        }

        private AtomicReadModelSignatureFixer GenerateSut()
        {
            return _container.Resolve<AtomicReadModelSignatureFixer>();
        }
    }
}
