using System;
using Jarvis.Framework.Shared.IdentitySupport;
using NUnit.Framework;

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
