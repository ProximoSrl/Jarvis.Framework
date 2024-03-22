using Jarvis.Framework.Shared.Helpers;
using Jarvis.Framework.TestHelpers;
using Jarvis.Framework.Tests.ProjectionsTests.Atomic.Support;
using NUnit.Framework;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Jarvis.Framework.Tests.ProjectionsTests.Atomic
{
    [TestFixture]
    public class AtomicProjectionEngineWithDbClean : AtomicProjectionEngineTestBase
    {
        public override void SetUp()
        {
            TestLogger.GlobalEnabled = true;
            base.SetUp();
            _db.Drop();
        }

        [Test]
        public async Task Verify_older_readmodel_will_use_a_different_cachup_projection_engine()
        {
            Int32 newCheckpoint = 110;
            var csAtomic = await GenerateAtomicAggregateCreatedEvent().ConfigureAwait(false);
            await GenerateSomeChangesetsAndReturnLatestsChangeset().ConfigureAwait(false);
            await GenerateEmptyUntil(newCheckpoint).ConfigureAwait(false);

            //And finally check if everything is projected
            _sut = await CreateSutAndStartProjectionEngineAsync().ConfigureAwait(false);

            //we need to wait to understand if it was projected
            GetTrackerAndWaitForChangesetToBeProjected("SimpleTestAtomicReadModel");

            await _sut.StopAsync().ConfigureAwait(false);

            //now we will drop everything, and recreate the projection service, this time we will have
            //another readmodel, that starts from 0, we do not want that readmodel to be projected
            //with the primary projection service.
            GenerateContainer(); //regenerate everything, we need to simulate a fresh start

            //recreate everythjing
            _sut = await CreateSutAndStartProjectionEngineAsync(autostart: false).ConfigureAwait(false);
            _sut.MaximumDifferenceForCatchupPoller = newCheckpoint - 10;
            _sut.RegisterAtomicReadModel(typeof(SimpleAtomicAggregateReadModel));

            await _sut.StartAsync(100, 100).ConfigureAwait(false);

            //ok, we do not want the main projection engine to register the new projection, then it should be projected with a different poller.
            _aggregateIdSeed++;
            await GenerateSomeChangesetsAndReturnLatestsChangeset().ConfigureAwait(false);

            await _sut.Poll();

            await GetTrackerAndWaitForChangesetToBeProjectedAsync("SimpleAtomicAggregateReadModel");
            await GetTrackerAndWaitForChangesetToBeProjectedAsync("SimpleTestAtomicReadModel");
            await _sut.StopAsync().ConfigureAwait(false);

            var readmodel = await _collectionForAtomicAggregate.FindOneByIdAsync(csAtomic.GetIdentity()).ConfigureAwait(false);
            Assert.That(readmodel, Is.Not.Null);

            //Verify logs dumped the catchup polle
            Assert.That(_loggerInstance.Logs.Any(_ => _.Level == Castle.Core.Logging.LoggerLevel.Info && _.Text.Contains("Catchup Poller started because some readmodel are too far behind")));
        }
    }
}
