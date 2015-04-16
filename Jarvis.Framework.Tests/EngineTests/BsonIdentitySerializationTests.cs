using System;
using System.Diagnostics;
using Jarvis.Framework.Shared.IdentitySupport;
using Jarvis.Framework.TestHelpers;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using NUnit.Framework;

namespace Jarvis.Framework.Tests.EngineTests
{
	[TestFixture]
	public class BsonIdentitySerializationTests
	{
		private SampleAggregateId _sampleAggregateId = new SampleAggregateId(1);

	    const string Expected =
            "{ \"MessageId\" : \"fc3e5f0a-c4f0-47d5-91cf-a1c87fee600f\" }";

	    [TestFixtureSetUp]
	    public void TestFixtureSetUp()
	    {
            IdentitiesRegistration.RegisterFromAssembly(GetType().Assembly);
        }

	    [Test]
		public void serialize_to_bson()
		{
			var evt = new SampleAggregateCreated()
				          {
					          MessageId=Guid.Parse("fc3e5f0a-c4f0-47d5-91cf-a1c87fee600f")
				          };

            evt.AssignIdForTest(_sampleAggregateId);

			var doc = evt.ToJson();
			Debug.WriteLine(doc);

			Assert.AreEqual(Expected, doc);
		}

		[Test]
		public void deserialize_from_bson()
		{
			var d = BsonSerializer.Deserialize<SampleAggregateCreated>(Expected);
            Assert.AreEqual(Guid.Parse("fc3e5f0a-c4f0-47d5-91cf-a1c87fee600f"), d.MessageId);
		}
	}
}