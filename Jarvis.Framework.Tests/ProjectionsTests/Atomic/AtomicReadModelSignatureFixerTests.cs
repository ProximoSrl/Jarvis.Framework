using Jarvis.Framework.Kernel.ProjectionEngine.Atomic;
using Jarvis.Framework.Shared.Events;
using Jarvis.Framework.Shared.Helpers;
using Jarvis.Framework.Shared.ReadModel.Atomic;
using Jarvis.Framework.Tests.ProjectionsTests.Atomic.Support;
using NStore.Domain;
using NUnit.Framework;
using NUnit.Framework.Internal.Commands;
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
            SimpleTestAtomicReadModel.FakeSignature = 1;
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

        [Test]
        public async Task Verify_event_for_end_fixing()
        {
            //Arrange: Generate some commit and project them
            SimpleTestAtomicReadModel.FakeSignature = 1;
            Changeset changeset = await GenerateSomeChangesetsAndReturnLatestsChangeset().ConfigureAwait(false);
            var engine = await CreateSutAndStartProjectionEngineAsync().ConfigureAwait(false);
            GetTrackerAndWaitForChangesetToBeProjected("SimpleTestAtomicReadModel");
            await engine.StopAsync().ConfigureAwait(false);

            //Act, start the fixer and change signature
            SimpleTestAtomicReadModel.FakeSignature = 2;
            var sut = GenerateSut();
            AtomicReadmodelFixedEventArgs atomicReadmodelFixedEventArgs = null;
            sut.ReadmodelFixed += (sender, args) =>
            {
                atomicReadmodelFixedEventArgs = args;
            };
            sut.AddReadmodelToFix(typeof(SimpleTestAtomicReadModel));
            sut.StartFixing();

            //ok I'm expecting the fix to correct the readmodel
            AssertForReadmodelCondition<SimpleTestAtomicReadModel>(changeset, rm => rm.ReadModelVersion == 2 && rm.TouchCount == 4);

            //event should be raised, wait for a little bit
            DateTime startWait = DateTime.UtcNow;
            while (DateTime.UtcNow.Subtract(startWait).TotalSeconds < 5)
            {
                if (atomicReadmodelFixedEventArgs != null)
                {
                    break;
                }
                Thread.Sleep(100);
            }

            Assert.That(atomicReadmodelFixedEventArgs, Is.Not.Null);
            Assert.That(atomicReadmodelFixedEventArgs.ReadmodelType, Is.EqualTo(typeof(SimpleTestAtomicReadModel)));
        }

        [Test]
        public async Task Verify_fix_for_readmodel_persists_faulted_with_new_version()
        {
            //Arrange: Generate some commit and project them
            SimpleTestAtomicReadModel.FakeSignature = 1;
            Changeset changeset = await GenerateSomeChangesetsAndReturnLatestsChangeset().ConfigureAwait(false);
            
            for (int i = 0; i < 10; i++)
            {
                await GenerateTouchedEvent();
            }
            var engine = await CreateSutAndStartProjectionEngineAsync().ConfigureAwait(false);
            GetTrackerAndWaitForChangesetToBeProjected("SimpleTestAtomicReadModel");
            await engine.StopAsync().ConfigureAwait(false);

            //Act, start the fixer and change signature
            SimpleTestAtomicReadModel.FakeSignature = 2;
            var sut = GenerateSut();
            sut.AddReadmodelToFix(typeof(SimpleTestAtomicReadModel));
            var actualTouchMax = SimpleTestAtomicReadModel.TouchMax;
            try
            {
                SimpleTestAtomicReadModel.TouchMax = 6;
                sut.StartFixing();

                //ok I'm expecting the fix to correct the readmodel
                //touch count increment with FakeSignature so we expect 3 touch events
                AssertForReadmodelCondition<SimpleTestAtomicReadModel>(
                    changeset,
                    rm => rm.ReadModelVersion == 2
                        && rm.Faulted
                        && rm.TouchCount == 6
                        && rm.AggregateVersion == 5, //first is creation, then 3 touches reach version 4, then in version 5 got faulted.
                    secondsToWait: 5);
            }
            finally
            {
                SimpleTestAtomicReadModel.TouchMax = actualTouchMax;
            }
        }

        /// <summary>
        /// Poll readmodel until the readmodel does not satisfy condition or timeout is reached.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="changeset"></param>
        /// <param name="conditionToAssert"></param>
        private void AssertForReadmodelCondition<T>(Changeset changeset, Func<T, Boolean> conditionToAssert, int secondsToWait = 5)
            where T : class, IAtomicReadModel
        {
            var firstEvent = changeset.Events[0] as DomainEvent;
            DateTime startWait = DateTime.UtcNow;
            var collection = GetCollection<T>();
            while (DateTime.UtcNow.Subtract(startWait).TotalSeconds < secondsToWait)
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
