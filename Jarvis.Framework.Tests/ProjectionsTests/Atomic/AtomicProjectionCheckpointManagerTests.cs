using Jarvis.Framework.Kernel.ProjectionEngine.Atomic;
using Jarvis.Framework.Shared.Exceptions;
using Jarvis.Framework.Shared.Helpers;
using Jarvis.Framework.Tests.ProjectionsTests.Atomic.Support;
using MongoDB.Driver;
using NUnit.Framework;
using System.Configuration;
using System.Threading.Tasks;

namespace Jarvis.Framework.Tests.ProjectionsTests.Atomic
{
    [TestFixture]
    public class AtomicProjectionCheckpointManagerTests
    {
        private IMongoDatabase _db;

        [OneTimeSetUp]
        public void OneTimeSetup()
        {
            var url = new MongoUrl(ConfigurationManager.ConnectionStrings["readmodel"].ConnectionString);
            var client = new MongoClient(url);
            _db = client.GetDatabase(url.DatabaseName);
        }

        [SetUp]
        public void SetUp()
        {
            _db.Drop();
        }

        private AtomicProjectionCheckpointManager GenerateSut()
        {
            return new AtomicProjectionCheckpointManager(_db);
        }

        [Test]
        public async Task Verify_basic_flush_and_reload()
        {
            var sut = GenerateSut();
            sut.Register(typeof(SimpleTestAtomicReadModel));
            sut.MarkPosition("SimpleTestAtomicReadModel", 42);
            await sut.FlushAsync().ConfigureAwait(false);

            //ok we need to verify that now, if we recreate another instance, everything is reloaded
            sut = GenerateSut();
            var checkpoint = sut.GetCheckpoint("SimpleTestAtomicReadModel");
            Assert.That(checkpoint, Is.EqualTo(42));
        }

        [Test]
        public async Task Verify_update_all()
        {
            var sut = GenerateSut();
            sut.Register(typeof(SimpleTestAtomicReadModel));
            sut.Register(typeof(AnotherSimpleTestAtomicReadModel));
            sut.MarkPositionToAllReadModel(42);

            var position1 = sut.GetCheckpoint("SimpleTestAtomicReadModel");
            var position2 = sut.GetCheckpoint("AnotherSimpleTestAtomicReadModel");
            //Verify position is updated
            Assert.That(position1, Is.EqualTo(42));
            Assert.That(position2, Is.EqualTo(42));

            //now flush
            await sut.FlushAsync().ConfigureAwait(false);

            //ok we need to verify that now, if we recreate another instance, everything is reloaded
            sut = GenerateSut();
            var checkpoint = sut.GetCheckpoint("SimpleTestAtomicReadModel");
            Assert.That(checkpoint, Is.EqualTo(42));

            checkpoint = sut.GetCheckpoint("AnotherSimpleTestAtomicReadModel");
            Assert.That(checkpoint, Is.EqualTo(42));
        }


        [Test]
        public void Does_not_throw_with_multiple_registration()
        {
            var sut = GenerateSut();
            sut.Register(typeof(SimpleTestAtomicReadModel));
            sut.Register(typeof(SimpleTestAtomicReadModel));
        }

        [Test]
        public void Throw_when_we_have_two_readmodel_with_same_name()
        {
            var sut = GenerateSut();
            sut.Register(typeof(SimpleTestAtomicReadModel));
            Assert.Throws<JarvisFrameworkEngineException>(() => sut.Register(typeof(SimpleTestAtomicReadModelWrong)));
        }

        [Test]
        public async Task Verify_basic_flush_and_reload_does_not_break_when_register_readmodel()
        {
            var sut = GenerateSut();
            sut.Register(typeof(SimpleTestAtomicReadModel));
            sut.MarkPosition("SimpleTestAtomicReadModel", 42);
            await sut.FlushAsync().ConfigureAwait(false);

            //ok we need to verify that now, if we recreate another instance, everything is reloaded
            sut = GenerateSut();
            var checkpoint = sut.GetCheckpoint("SimpleTestAtomicReadModel");
            sut.Register(typeof(SimpleTestAtomicReadModel));
            Assert.That(checkpoint, Is.EqualTo(42));
        }

        [Test]
        public void Mark_does_not_made_checkpoint_to_go_backward()
        {
            var sut = GenerateSut();
            sut.Register(typeof(SimpleTestAtomicReadModel));
            sut.MarkPosition("SimpleTestAtomicReadModel", 1);
            sut.MarkPosition("SimpleTestAtomicReadModel", 34);
            Assert.That(sut.GetCheckpoint("SimpleTestAtomicReadModel"), Is.EqualTo(34));

            sut.MarkPosition("SimpleTestAtomicReadModel", 20);
            Assert.That(sut.GetCheckpoint("SimpleTestAtomicReadModel"), Is.EqualTo(34), "Telling that we dispatched 20 should not update checkpoint");
        }
    }
}
