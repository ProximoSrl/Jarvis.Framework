using Castle.Core.Logging;
using Jarvis.Framework.Bus.Rebus.Integration.Support;
using Jarvis.Framework.Shared.Domain.Serialization;
using Jarvis.Framework.Shared.Messages;
using Newtonsoft.Json;
using NUnit.Framework;
using System;
using System.Collections.Generic;

namespace Jarvis.Framework.Tests.BusTests
{
    [TestFixture]
    [Category("Serialization")]
    public class JarvisFrameworkRebusSerializationBinderTests
    {
        private JsonSerializerSettings GetSettingsForTest() => new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.All,
            TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Full,
            ContractResolver = new MessagesContractResolver(),
            ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor,
            Converters = new JsonConverter[]
            {
                new StringValueJsonConverter()
            },
            SerializationBinder = new JarvisFrameworkRebusSerializationBinder(NullLogger.Instance)
        };

        [Test]
        public void Dictionary_serialization()
        {
            TestSerializationDictionary obj = new TestSerializationDictionary();
            var serialized = JsonConvert.SerializeObject(obj, GetSettingsForTest());
            Assert.That(!serialized.Contains("System.Private.CoreLib"));
        }

        [Test]
        public void Dictionary_serialization_nested()
        {
            var obj = new TestSerializationDictionaryNested();
            var serialized = JsonConvert.SerializeObject(obj, GetSettingsForTest());
            Assert.That(!serialized.Contains("System.Private.CoreLib"));
        }

        [Test]
        public void Class_with_Array()
        {
            var obj = new ClassWithArray();
            obj.DomainEvents = new[] { "Test" };
            var serialized = JsonConvert.SerializeObject(obj, GetSettingsForTest());
            Console.WriteLine(serialized);
            Assert.That(!serialized.Contains("System.Private.CoreLib"));
        }

        private class TestSerializationDictionary
        {
            public Dictionary<String, String[]> DictionaryWithArray { get; set; } = new Dictionary<string, string[]>();
        }

        private class TestSerializationDictionaryNested
        {
            public Dictionary<String, Dictionary<string, string[]>> DictionaryWithArray { get; set; } = new Dictionary<string, Dictionary<string, string[]>>();
        }

        public class ClassWithArray
        {
            public String[] DomainEvents { get; set; }
        }
    }
}
