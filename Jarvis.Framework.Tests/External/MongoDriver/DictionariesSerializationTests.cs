using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.Options;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jarvis.Framework.Tests.External.MongoDriver
{
    [TestFixture]
    public class DictionariesSerializationTests
    {
        private IMongoDatabase db;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            var connectionString = ConfigurationManager.ConnectionStrings["readmodel"].ConnectionString;
            var url = new MongoUrl(connectionString);
            var client = new MongoClient(url);
            db = client.GetDatabase(url.DatabaseName);

        }

        private class TestClassWithHeadersAsConcreteDictionary
        {
            public Dictionary<String, String> Headers { get; set; }

            public String Property { get; set; }
        }

        [Test]
        public void Verify_ability_to_map_with_code_dictionary_representation()
        {
            BsonClassMap.RegisterClassMap<TestClassWithHeadersAsConcreteDictionary>(map =>
            {
                map.AutoMap();
                map.MapProperty(c => c.Headers).SetSerializer(
                    new DictionaryInterfaceImplementerSerializer<Dictionary<String, String>>(DictionaryRepresentation.ArrayOfArrays));
            });

            var collection = db.GetCollection<TestClassWithHeadersAsConcreteDictionary>("ConcreteDictionary");

            var instance = new TestClassWithHeadersAsConcreteDictionary()
            {
                Headers = new Dictionary<string, string>()
                {
                    ["with.dot"] = "value",
                },
                Property = "Property value"
            };
            collection.InsertOne(instance);
        }

        private class TestClassWithHeadersAsConcreteDictionaryMappedWithAttributes
        {
            [BsonDictionaryOptionsAttribute(DictionaryRepresentation.ArrayOfArrays)]
            public Dictionary<String, String> Headers { get; set; }

            public String Property { get; set; }
        }

        [Test]
        public void Verify_ability_to_map_with_attribute_dictionary_representation()
        {
            var collection = db.GetCollection<TestClassWithHeadersAsConcreteDictionaryMappedWithAttributes>("ConcreteDictionaryMapped");

            var instance = new TestClassWithHeadersAsConcreteDictionaryMappedWithAttributes()
            {
                Headers = new Dictionary<string, string>()
                {
                    ["with.dot"] = "value",
                },
                Property = "Property value"
            };
            collection.InsertOne(instance);
        }

        private class TestClassWithHeadersAsInterfaceDictionary
        {
            public IDictionary<String, String> Headers { get; set; }

            public String Property { get; set; }
        }

        [Test]
		[Explicit("This test shows a bug in mongo mapping.")]
        public void Verify_ability_to_map_with_code_interface_dictionary_representation()
        {
            BsonClassMap.RegisterClassMap<TestClassWithHeadersAsInterfaceDictionary>(map =>
            {
                map.AutoMap();
                var dictionarySerializer = new DictionaryInterfaceImplementerSerializer<Dictionary<String, String>>(DictionaryRepresentation.ArrayOfArrays);
                var headerMapper = new ImpliedImplementationInterfaceSerializer<IDictionary<String, String>, Dictionary<String, String>>(dictionarySerializer);
                map.MapProperty(c => c.Headers).SetSerializer(headerMapper);
            });

            var collection = db.GetCollection<TestClassWithHeadersAsInterfaceDictionary>("InterfaceDictionary");

            //This is good, because we are using Dictionary
            var instance = new TestClassWithHeadersAsInterfaceDictionary()
            {
                Headers = new Dictionary<string, string>()
                {
                    ["with.dot"] = "value",
                },
                Property = "Property value"
            };
            collection.InsertOne(instance);

            //This also should be good, but a bug in the driver prevent it to be saved
            instance = new TestClassWithHeadersAsInterfaceDictionary()
            {
                Headers = new SortedDictionary<string, string>()
                {
                    ["with.dot"] = "value",
                },
                Property = "Property value"
            };
            collection.InsertOne(instance);
        }

        private class TestClassWithHeadersAsInterfaceDictionaryMappedWithAttributes
        {
            [BsonDictionaryOptionsAttribute(DictionaryRepresentation.ArrayOfArrays)]
            public IDictionary<String, String> Headers { get; set; }

            public String Property { get; set; }
        }

        [Test]
        public void Verify_ability_to_map_with_attribute_interface_dictionary_representation()
        {
            var collection = db.GetCollection<TestClassWithHeadersAsInterfaceDictionaryMappedWithAttributes>("InterfaceDictionary");

            var instance = new TestClassWithHeadersAsInterfaceDictionaryMappedWithAttributes()
            {
                Headers = new Dictionary<string, string>()
                {
                    ["with.dot"] = "value",
                },
                Property = "Property value"
            };
            collection.InsertOne(instance);

            var mappingOfHeaders = BsonClassMap.GetRegisteredClassMaps()
                .Single(m => m.ClassType == typeof(TestClassWithHeadersAsInterfaceDictionaryMappedWithAttributes))
                .AllMemberMaps
                .Single(mm => mm.MemberName == "Headers");
            Console.WriteLine($"Mapping is {mappingOfHeaders.GetType()}");
        }
    }
}
