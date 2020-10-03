using Jarvis.Framework.Kernel.ProjectionEngine;
using Jarvis.Framework.Tests.ProjectionEngineTests;
using MongoDB.Driver;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Jarvis.Framework.Shared.Helpers;

namespace Jarvis.Framework.Tests.ProjectionsTests
{
    [TestFixture(true)]
    [TestFixture(false)]
    public class CachedMongoStorageTests
    {
        private readonly Boolean _inMemory;

        public CachedMongoStorageTests(Boolean inMemory)
        {
            _inMemory = inMemory;
        }

        private IMongoCollection<SampleReadModel4> _collection;

        [OneTimeSetUp]
        public void TestFixtureSetUp()
        {
            var url = new MongoUrl(ConfigurationManager.ConnectionStrings["readmodel"].ConnectionString);
            var client = new MongoClient(url);
            var readmodelDb = client.GetDatabase(url.DatabaseName);
            _collection = readmodelDb.GetCollection<SampleReadModel4>("SampleReadModel4");
        }

        [Test]
        public async Task Basic_insert_and_retrieve_in_memory()
        {
            var sut = CreateSut();
            await sut.InsertAsync(new SampleReadModel4() { Id = "1", Name = "A Name" }).ConfigureAwait(false);
            var loaded = await sut.FindOneByIdAsync("1").ConfigureAwait(false);
            Assert.That(loaded.Name, Is.EqualTo("A Name"));
        }

        [Test]
        public async Task Create_index_then_add_an_element_element()
        {
            var sut = CreateSut();
            //This will force creation of the idnex.
            await sut.FindByPropertyAsListAsync(e => e.Name, null).ConfigureAwait(false); //Forces index creation.
            //Then insert the element
            await sut.InsertAsync(new SampleReadModel4() { Id = "1", Name = "A Name" }).ConfigureAwait(false);
            await sut.InsertAsync(new SampleReadModel4() { Id = "2", Name = "Another Name" }).ConfigureAwait(false);
            //Retrieve element with index
            var loaded = await sut.FindByPropertyAsListAsync(e => e.Name, "A Name").ConfigureAwait(false);
            Assert.That(loaded.Count, Is.EqualTo(1));
            Assert.That(loaded[0].Id, Is.EqualTo("1"));
        }

        [Test]
        public async Task Add_element_then_create_index()
        {
            var sut = CreateSut();

            //insert the element
            await sut.InsertAsync(new SampleReadModel4() { Id = "1", Name = "A Name" }).ConfigureAwait(false);
            await sut.InsertAsync(new SampleReadModel4() { Id = "2", Name = "Another Name" }).ConfigureAwait(false);

            //This will force creation of the idnex.
            await sut.FindByPropertyAsListAsync(e => e.Name, null).ConfigureAwait(false); //Forces index creation.

            //Retrieve element with index
            var loaded = await sut.FindByPropertyAsListAsync(e => e.Name, "A Name").ConfigureAwait(false);
            Assert.That(loaded.Count, Is.EqualTo(1));
            Assert.That(loaded[0].Id, Is.EqualTo("1"));
        }

        [Test]
        public async Task Basic_insert_and_retrieve_unexistent_in_memory()
        {
            var sut = CreateSut();
            await sut.InsertAsync(new SampleReadModel4() { Id = "1", Name = "A Name" }).ConfigureAwait(false);
            var loaded = await sut.FindOneByIdAsync("2").ConfigureAwait(false);
            Assert.That(loaded, Is.EqualTo(null));
        }

        [Test]
        public async Task Delete_correctly_remove_element_from_index()
        {
            var sut = CreateSut();

            //insert the element
            await sut.InsertAsync(new SampleReadModel4() { Id = "1", Name = "A Name" }).ConfigureAwait(false);
            await sut.InsertAsync(new SampleReadModel4() { Id = "2", Name = "Another Name" }).ConfigureAwait(false);

            //This will force creation of the idnex.
            await sut.FindByPropertyAsListAsync(e => e.Name, null).ConfigureAwait(false); //Forces index creation.

            //remove element
            await sut.DeleteAsync("1").ConfigureAwait(false);

            //element should not be present
            var loaded = await sut.FindByPropertyAsListAsync(e => e.Name, "A Name").ConfigureAwait(false);
            Assert.That(loaded.Count, Is.EqualTo(0));
        }

        [Test]
        public async Task verify_that_null_is_indexed()
        {
            var sut = CreateSut();
            await sut.InsertAsync(new SampleReadModel4() { Id = "1", Name = "A Name" }).ConfigureAwait(false);
            await sut.InsertAsync(new SampleReadModel4() { Id = "2", Name = null }).ConfigureAwait(false);
            var element = await sut.FindByPropertyAsListAsync(e => e.Name, null).ConfigureAwait(false); //This will force index creation
            Assert.That(element.Single().Id, Is.EqualTo("2"));
        }

        [Test]
        public async Task verify_that_null_correctly_updates()
        {
            var sut = CreateSut();
            await sut.InsertAsync(new SampleReadModel4() { Id = "1", Name = "A Name" }).ConfigureAwait(false);
            SampleReadModel4 readModel = new SampleReadModel4() { Id = "2", Name = null };
            await sut.InsertAsync(readModel).ConfigureAwait(false);
            await sut.FindByPropertyAsListAsync(e => e.Name, null).ConfigureAwait(false); //Forces index creation.

            readModel.Name = "Another Name";
            await sut.SaveWithVersionAsync(readModel, readModel.Version).ConfigureAwait(false);
            var element = await sut.FindByPropertyAsListAsync(e => e.Name, null).ConfigureAwait(false);
            Assert.That(element, Is.Empty);

            element = await sut.FindByPropertyAsListAsync(e => e.Name, "Another Name").ConfigureAwait(false);
            Assert.That(element.Single().Id, Is.EqualTo(readModel.Id));
        }

        [Test]
        public async Task Insert_Batch_and_retrieve_with_property()
        {
            var sut = CreateSut();
            await sut.FindByPropertyAsListAsync(e => e.Value, 12).ConfigureAwait(false); //This will force index creation
            await sut.InsertBatchAsync(new[] {
                new SampleReadModel4() { Id = "1", Value = 1 },
                new SampleReadModel4() { Id = "2", Value = 2 },
                new SampleReadModel4() { Id = "3", Value = 2 },
                new SampleReadModel4() { Id = "4", Value = 4 },
            }).ConfigureAwait(false);
            var loaded = (await sut.FindByPropertyAsListAsync(e => e.Value, 2).ConfigureAwait(false)).ToList();
            Assert.That(loaded.All(e => e.Value == 2));
            Assert.That(loaded, Has.Count.EqualTo(2));
        }

        [Test]
        public async Task Insert_Batch_and_delete_then_retrieve_with_property()
        {
            var sut = CreateSut();
            await sut.FindByPropertyAsListAsync(e => e.Value, 12).ConfigureAwait(false); //This will force index creation
            await sut.InsertBatchAsync(new[] {
                new SampleReadModel4() { Id = "1", Value = 1 },
                new SampleReadModel4() { Id = "2", Value = 2 },
                new SampleReadModel4() { Id = "3", Value = 2 },
                new SampleReadModel4() { Id = "4", Value = 4 },
            }).ConfigureAwait(false);
            await sut.DeleteAsync("3").ConfigureAwait(false);
            var loaded = (await sut.FindByPropertyAsListAsync(e => e.Value, 2).ConfigureAwait(false)).ToList();
            Assert.That(loaded.All(e => e.Value == 2));
            Assert.That(loaded, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task Insert_then_change_property()
        {
            var sut = CreateSut();
            await sut.FindByPropertyAsListAsync(e => e.Value, 12).ConfigureAwait(false); //This will force index creation
            await sut.InsertBatchAsync(new[] {
                new SampleReadModel4() { Id = "1", Value = 2 },
                new SampleReadModel4() { Id = "2", Value = 2 },
            }).ConfigureAwait(false);

            var loaded = (await sut.FindByPropertyAsListAsync(e => e.Value, 2).ConfigureAwait(false)).ToList();
            Assert.That(loaded.All(e => e.Value == 2));
            Assert.That(loaded, Has.Count.EqualTo(2));
            var first = loaded[0];
            first.Value = 3;
            await sut.SaveWithVersionAsync(first, first.Version).ConfigureAwait(false);

            loaded = (await sut.FindByPropertyAsListAsync(e => e.Value, 2).ConfigureAwait(false)).ToList();
            Assert.That(loaded, Has.Count.EqualTo(1));
            Assert.That(loaded[0].Id, Is.EqualTo("2"));

            loaded = (await sut.FindByPropertyAsListAsync(e => e.Value, 3).ConfigureAwait(false)).ToList();
            Assert.That(loaded, Has.Count.EqualTo(1));
            Assert.That(loaded[0].Id, Is.EqualTo("1"));
        }

        [Test]
        public async Task insert_many_and_retrieve_with_and_without_index()
        {
            if (!_inMemory) return; //this test makes sense only in memory

            var sut = CreateSut();
            Int32 iteration = 100000;
            for (int i = 0; i < iteration; i++)
            {
                await sut.InsertAsync(new SampleReadModel4() { Id = i.ToString(), Name = "Element: " + i, Value = i % 100 }).ConfigureAwait(false);
            }

            Int32 count = 0;
            Stopwatch sw = new Stopwatch();
            sw.Start();
            foreach (var element in sut.All.Where(e => e.Value == 42))
            {
                //do something with element
                count++;
            }
            sw.Stop();
            Console.WriteLine("ALL: {0}", sw.ElapsedMilliseconds);
            Assert.That(count, Is.EqualTo(iteration / 100));

            count = 0;
            sw.Reset();
            sw.Start();
            foreach (var element in await sut.FindByPropertyAsListAsync(e => e.Value, 42).ConfigureAwait(false))
            {
                //do something with element
                count++;
            }
            sw.Stop();
            Console.WriteLine("FirstIndex: {0}", sw.ElapsedMilliseconds);
            Assert.That(count, Is.EqualTo(iteration / 100));

            count = 0;
            sw.Reset();
            sw.Start();
            foreach (var element in await sut.FindByPropertyAsListAsync(e => e.Value, 42).ConfigureAwait(false))
            {
                //do something with element
                count++;
            }
            sw.Stop();
            Console.WriteLine("SecondIndex: {0}", sw.ElapsedMilliseconds);
            Assert.That(count, Is.EqualTo(iteration / 100));
        }

        [Test]
        public async Task insert_many_query_with_property_Then_insert_other()
        {
            if (!_inMemory) return; //this test makes sense only in memory

            var sut = CreateSut();
            Int32 iteration = 100000;
            Int32 count = 0;
            Stopwatch sw = new Stopwatch();

            sw.Start();
            for (int i = 0; i < iteration; i++)
            {
                await sut.InsertAsync(new SampleReadModel4() { Id = i.ToString(), Name = "Element: " + i, Value = i % 100 }).ConfigureAwait(false);
            }
            sw.Stop();
            Console.WriteLine("First Insert: {0}", sw.ElapsedMilliseconds);

            sw.Restart();
            foreach (var element in await sut.FindByPropertyAsListAsync(e => e.Value, 42).ConfigureAwait(false))
            {
                //do something with element
                count++;
            }
            sw.Stop();
            Console.WriteLine("ALL first run: {0}", sw.ElapsedMilliseconds);
            Assert.That(count, Is.EqualTo(iteration / 100));
            count = 0;

            sw.Restart();
            for (int i = iteration; i < iteration * 2; i++)
            {
                await sut.InsertAsync(new SampleReadModel4() { Id = i.ToString(), Name = "Element: " + i, Value = i % 100 }).ConfigureAwait(false);
            }
            sw.Stop();
            Console.WriteLine("Second Insert: {0}", sw.ElapsedMilliseconds);

            sw.Restart();
            foreach (var element in await sut.FindByPropertyAsListAsync(e => e.Value, 42))
            {
                //do something with element
                count++;
            }
            sw.Stop();
            Console.WriteLine("ALL second run: {0}", sw.ElapsedMilliseconds);
            Assert.That(count, Is.EqualTo(iteration * 2 / 100));
            count = 0;

            sw.Restart();
            foreach (var element in sut.All.Where(e => e.Value == 42))
            {
                //do something with element
                count++;
            }
            sw.Stop();
            Console.WriteLine("ALL second run without index: {0}", sw.ElapsedMilliseconds);
            Assert.That(count, Is.EqualTo(iteration * 2 / 100));
        }

        private CachedMongoStorage<SampleReadModel4, String> CreateSut()
        {
            _collection.Drop();
            var inmemoryCollection = new InmemoryCollection<SampleReadModel4, string>();
            if (_inMemory) inmemoryCollection.Activate();
            return new CachedMongoStorage<SampleReadModel4, string>(
                _collection,
                inmemoryCollection);
        }
    }
}
