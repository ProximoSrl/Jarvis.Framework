using Jarvis.Framework.Shared.Domain.Serialization;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using NUnit.Framework;

namespace Jarvis.Framework.Tests.DomainTests
{
    [TestFixture]
    public class StringValueMongoTests
    {
        [Test]
        public void should_serialize()
        {
            var instance = new ClassWithTypedStringValue { Value = new TypedStringValue("abc_123") };
            var json = instance.ToJson();

            Assert.AreEqual("{ \"Value\" : \"abc_123\" }", json);
        }

        [Test]
        public void should_deserialize()
        {
            var instance = BsonSerializer.Deserialize<ClassWithTypedStringValue>("{ Value:\"abc_123\"}");
            Assert.AreEqual("abc_123", (string)instance.Value);
        }

        [Test]
        public void should_serialize_null()
        {
            var instance = new ClassWithTypedStringValue();
            var json = instance.ToJson();
            Assert.AreEqual("{ \"Value\" : null }", json);
        }

        [Test]
        public void should_deserialize_null()
        {
            var instance = BsonSerializer.Deserialize<ClassWithTypedStringValue>("{ Value: null}");
            Assert.IsNull(instance.Value);
        }

        [Test]
        public void can_convert_to_bson_value()
        {
            StringValueCustomBsonTypeMapper.Register<TypedStringValue>();
            var val = BsonValue.Create(new TypedStringValue("abc"));
            Assert.IsInstanceOf<BsonString>(val);
        }
    }
}
