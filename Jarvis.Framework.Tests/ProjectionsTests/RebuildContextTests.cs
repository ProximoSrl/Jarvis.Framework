using System.Linq;
using Jarvis.Framework.Kernel.ProjectionEngine;
using Jarvis.Framework.Shared.ReadModel;
using NUnit.Framework;

namespace Jarvis.Framework.Tests.ProjectionsTests
{
    [TestFixture]
    public class RebuildContextTests
    {
        RebuildContext _context;
        IInmemoryCollection<SampleReadModelWithStringKey, string> _inmemoryCollection;

        private class SampleReadModelWithStringKey : AbstractReadModel<string>
        {
        }

        [SetUp]
        public void SetUp()
        {
            _context = new RebuildContext(false);
            _inmemoryCollection = _context.GetCollection<SampleReadModelWithStringKey, string>("sample", false);
        }

        [Test]
        public void can_store()
        {
            var model = new SampleReadModelWithStringKey()
            {
                Id = "a"
            };
            _inmemoryCollection.Save(model);
            NUnit.Framework.Legacy.ClassicAssert.AreEqual(1, _inmemoryCollection.GetAll().Count());
        }

        [Test]
        public void can_store_and_retrieve_model()
        {
            var model = new SampleReadModelWithStringKey()
            {
                Id = "a"
            };
            _inmemoryCollection.Save(model);

            var loaded = _inmemoryCollection.GetById("a");

            NUnit.Framework.Legacy.ClassicAssert.AreSame(model, loaded);
            NUnit.Framework.Legacy.ClassicAssert.AreEqual(1, _inmemoryCollection.GetAll().Count());
        }

        [Test]
        public void can_delete_by_id()
        {
            var model = new SampleReadModelWithStringKey()
            {
                Id = "a"
            };
            _inmemoryCollection.Save(model);

            _inmemoryCollection.Delete("a");

            NUnit.Framework.Legacy.ClassicAssert.AreEqual(0, _inmemoryCollection.GetAll().Count());
        }

        [Test]
        public void clear_should_remove_all_models()
        {
            var model = new SampleReadModelWithStringKey(){
                Id = "a"
            };
            
            _inmemoryCollection.Save(model);
            
            _inmemoryCollection.Clear();

            NUnit.Framework.Legacy.ClassicAssert.AreEqual(0, _inmemoryCollection.GetAll().Count());
        }

        [Test]
        public void should_throw_if_duplicated_id()
        {
            var model = new SampleReadModelWithStringKey()
            {
                Id = "a"
            };

            _inmemoryCollection.Insert(model);

            var ex = Assert.Throws<DuplicatedElementException>(() => _inmemoryCollection.Insert(model));
            NUnit.Framework.Legacy.ClassicAssert.AreEqual("Duplicated element with id a", ex.Message);
        }
    }
}
