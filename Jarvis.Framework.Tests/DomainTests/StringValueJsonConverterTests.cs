using Jarvis.Framework.Shared.Domain.Serialization;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Jarvis.Framework.Tests.DomainTests
{
    [TestFixture]
    public class StringValueJsonConverterTests
    {
        JsonSerializerSettings _settings;

        [TestFixtureSetUp]
        public void TestFixtureSetUp()
        {
            _settings = new JsonSerializerSettings()
            {
                Converters = new JsonConverter[]
                {
                    new StringValueJsonConverter()
                }
            };
        }

        [Test]
        public void should_serialize()
        {
            var instance = new ClassWithTypedStringValue { Value = new TypedStringValue("abc_123") };
            var json = JsonConvert.SerializeObject(instance, _settings);

            Assert.AreEqual("{\"Value\":\"abc_123\"}", json);
        }

        [Test]
        public void should_deserialize()
        {
            var instance = JsonConvert.DeserializeObject<ClassWithTypedStringValue>("{ Value:\"abc_123\"}",_settings);
            Assert.AreEqual("abc_123", (string)instance.Value);
        }

        [Test]
        public void should_serialize_null()
        {
            var instance = new ClassWithTypedStringValue();
            var json = JsonConvert.SerializeObject(instance, _settings);
            Assert.AreEqual("{\"Value\":null}", json);
        }

        [Test]
        public void should_deserialize_null()
        {
            var instance = JsonConvert.DeserializeObject<ClassWithTypedStringValue>("{ AnId:null}", _settings);
            Assert.IsNull(instance.Value);
        }
    
    }
}