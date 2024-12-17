using Jarvis.Framework.Shared.IdentitySupport.Serialization;
using Jarvis.Framework.Tests.Support;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Configuration;

namespace Jarvis.Framework.Tests.SharedTests.IdentitySupport;

[TestFixture]
public class DictionaryAsObjectJarvisSerializerTests
{
    private IMongoDatabase _db;
    private IMongoCollection<TestSerialization> _collection;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _db = TestHelper.CreateNew(ConfigurationManager.ConnectionStrings["system"].ConnectionString);
        _collection = _db.GetCollection<TestSerialization>("test");
    }

    [Test]
    public void SerializeAndDeserialize()
    {
        var sut = new TestSerialization
        {
            Id = "1",
            MetadataValues = new Dictionary<string, ContainerMetadataValueReadModel>
            {
                { "key1", new ContainerMetadataValueReadModel { MetadataId = "1", Key = "key1", Value = "value1" } },
                { "key2", new ContainerMetadataValueReadModel { MetadataId = "2", Key = "key2", Value = "value2" } }
            }
        };

        _collection.InsertOne(sut);

        var result = _collection.Find(x => x.Id == "1").Single();

        Assert.That(result.MetadataValues.Count, Is.EqualTo(2));
        Assert.That(result.MetadataValues["key1"].MetadataId, Is.EqualTo("1"));
        Assert.That(result.MetadataValues["key1"].Key, Is.EqualTo("key1"));
    }

    public class TestSerialization
    {
        public string Id { get; set; }

        [BsonSerializer(typeof(DictionaryAsObjectJarvisSerializer<Dictionary<string, ContainerMetadataValueReadModel>, String, ContainerMetadataValueReadModel>))]
        public Dictionary<string, ContainerMetadataValueReadModel> MetadataValues { get; set; } = new Dictionary<string, ContainerMetadataValueReadModel>();
    }

    public class ContainerMetadataValueReadModel
    {
        public String MetadataId { get; set; }

        public String Key { get; set; }

        public String Value { get; set; }
    }
}
