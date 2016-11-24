using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Jarvis.NEventStoreEx.CommonDomainEx;
using Jarvis.NEventStoreEx.CommonDomainEx.Persistence;
using NUnit.Core;
using NUnit.Framework;
using NSubstitute;

namespace Jarvis.Framework.Tests.NeventStoreExTests.Persistence
{
    [TestFixture]
    public class ShapshotPersistenceStrategiesTests
    {
        [TestCase(50, 50, 1, true)]
        [TestCase(50, 51, 2, true)]
        [TestCase(50, 52, 2, false)]
        [TestCase(50, 70, 10, false)]
        [TestCase(50, 70, 40, true)]
        public void Verify_NumberOfCommitsShapshotPersistenceStrategy(Int32 treshold, Int32 version, Int32 numberOfEventsSaved, Boolean expected)
        {
            NumberOfCommitsShapshotPersistenceStrategy sut = new NumberOfCommitsShapshotPersistenceStrategy(treshold);
            IAggregateEx aggregate = NSubstitute.Substitute.For<IAggregateEx>();
            aggregate.Version.ReturnsForAnyArgs(version);
            var result = sut.ShouldSnapshot(aggregate, numberOfEventsSaved);
            Assert.That(result, Is.EqualTo(expected));
        }
    }
}
