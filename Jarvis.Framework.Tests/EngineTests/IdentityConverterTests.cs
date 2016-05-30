using System;
using Jarvis.Framework.Shared.IdentitySupport;
using NUnit.Framework;
using System.Diagnostics;
using System.Collections.Generic;

namespace Jarvis.Framework.Tests.EngineTests
{
	[TestFixture]
	public class IdentityConverterTests 
	{
		private IdentityManager _manager;

		[TestFixtureSetUp]
		public void TestFixtureSetUp()
		{
			_manager = new IdentityManager(new InMemoryCounterService());
			_manager.RegisterIdentitiesFromAssembly(GetType().Assembly);
		}

		[Test]
		public void convert_to_string()
		{
			var id = new SampleAggregateId(1);
			var asString = _manager.ToString(id);

			Assert.AreEqual("SampleAggregate_1", asString);
		}

		[Test]
		public void convert_to_identity()
		{
			var asString = EventStoreIdentity.Format(typeof(SampleAggregateId), 1L);
			var identity = (EventStoreIdentity)_manager.ToIdentity(asString);

			Assert.IsTrue(identity is SampleAggregateId);
			Assert.AreEqual("SampleAggregate", identity.GetTag());
			Assert.AreEqual(1L, identity.Id);
		}


        [Test, Explicit]
        public void Conver_lots_of_identities()
        {
            Stopwatch timer = new Stopwatch();
            List<String> test = new List<String>();
            _manager.ToIdentity("SampleAggregate_2");
            for (int i = 0; i < 1000000; i++)
            {
                test.Add(new SampleAggregateId(i).ToString());
            }
            timer.Start();
            foreach (var item in test)
            {
                _manager.ToIdentity(item);
            }
            timer.Stop();
            Console.WriteLine("Elapsed: {0}", timer.ElapsedMilliseconds);
        }

	    [Test]
	    public void as_string_and_to_string_are_equal()
	    {
	        var id = new SampleAggregateId(555);

	        var asString = id.AsString();
	        var toString = id.ToString();

            Assert.AreEqual(asString, toString);
	    }

	    [Test]
	    public void null_string_should_be_transalted_to_null_identity()
	    {
	        var identity = _manager.ToIdentity(null);

            Assert.IsNull(identity);
	    } 
        
        [Test]
	    public void empty_string_should_be_transalted_to_null_identity()
	    {
	        var identity = _manager.ToIdentity(String.Empty);

            Assert.IsNull(identity);
	    }

	    [Test]
	    public void whitespace_identity_should_throw_exception()
	    {
	        var ex = Assert.Catch<Exception>(() => _manager.ToIdentity(" "));

            Assert.AreEqual("invalid identity value  ", ex.Message);
        }

        [Test]
	    public void unknown_identity_should_throw_exception()
	    {
	        var ex = Assert.Catch<Exception>(() => _manager.ToIdentity("pluto_1"));

            Assert.AreEqual("pluto not registered in IdentityManager", ex.Message);
        }
	}
}
