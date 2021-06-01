using Castle.Core.Logging;
using Jarvis.Framework.Bus.Rebus.Integration.Support;
using Jarvis.Framework.Shared.Domain.Serialization;
using Jarvis.Framework.Shared.Exceptions;
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

        private JsonSerializerSettings GetDefaultSettings() => new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.None,
            TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Simple,
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

        [Test]
        public void Full_Serialization_survive_update_version()
        {
            Dictionary<String, ClassWithArray> dic = new Dictionary<string, ClassWithArray>();
            var obj = new ClassWithArray();
            obj.DomainEvents = new[] { "Test" };
            dic["test"] = obj;
            var serialized = JsonConvert.SerializeObject(dic, GetSettingsForTest());

            //Simulate that the serialization was done by an older version of the framework.
            var oldVersion = serialized.Replace("Jarvis.Framework.Tests, Version=1.0.0.0", "Jarvis.Framework.Tests, Version=0.9.0.0");

            //we can survive update version
            var deserialized = JsonConvert.DeserializeObject<Dictionary<String, ClassWithArray>>(oldVersion, GetSettingsForTest());
            Assert.That(deserialized["test"].DomainEvents, Is.EquivalentTo(new[] { "Test"}));
        }

        [Test]
        public void Full_Serialization_survive_update_change_assembly()
        {
            Dictionary<String, ClassWithArray> dic = new Dictionary<string, ClassWithArray>();
            var obj = new ClassWithArray();
            obj.DomainEvents = new[] { "Test" };
            dic["test"] = obj;
            var serialized = JsonConvert.SerializeObject(dic, GetSettingsForTest());

            Console.WriteLine(serialized);
            //Simulate that the serialization was done by an older version of the framework.
            var oldVersion = serialized.Replace("Jarvis.Framework.Tests, Version=1.0.0.0", "Jarvis.Framework.OldAssembly, Version=1.0.0.0");
            Console.WriteLine(oldVersion);

            Assert.Throws<JsonSerializationException>(() => JsonConvert.DeserializeObject<Dictionary<String, ClassWithArray>>(oldVersion, GetSettingsForTest()));

            //we can survive update version only with default
            var deserialized = JsonConvert.DeserializeObject<Dictionary<String, ClassWithArray>>(oldVersion, GetDefaultSettings());
            Assert.That(deserialized["test"].DomainEvents, Is.EquivalentTo(new[] { "Test" }));
        }

        [Test]
        public void Full_Serialization_survive_update_change_namespace()
        {
            Dictionary<String, ClassWithArray> dic = new Dictionary<string, ClassWithArray>();
            var obj = new ClassWithArray();
            obj.DomainEvents = new[] { "Test" };
            dic["test"] = obj;
            var serialized = JsonConvert.SerializeObject(dic, GetSettingsForTest());

            Console.WriteLine(serialized);
            //Simulate that the serialization was done by an older version of the framework.
            var oldVersion = serialized.Replace(
                "Jarvis.Framework.Tests.BusTests.JarvisFrameworkRebusSerializationBinderTests+ClassWithArray", 
                "Jarvis.Framework.Tests.This_was_in_an_old_namespace.JarvisFrameworkRebusSerializationBinderTests+ClassWithArray");
            Console.WriteLine(oldVersion);

            //Cannot survive deserialization with full information
            Assert.Throws<JsonSerializationException>(() => JsonConvert.DeserializeObject<Dictionary<String, ClassWithArray>>(oldVersion, GetSettingsForTest()));

            //we can survive update version only with default
            var deserialized = JsonConvert.DeserializeObject<Dictionary<String, ClassWithArray>>(oldVersion, GetDefaultSettings());
            Assert.That(deserialized["test"].DomainEvents, Is.EquivalentTo(new[] { "Test" }));
        }

        [Test]
        public void Try_mess_with_serialization_changing_almost_everything()
        {
            Dictionary<String, ClassWithArray> dic = new Dictionary<string, ClassWithArray>();
            var obj = new ClassWithArray();
            obj.DomainEvents = new[] { "Test" };
            dic["test"] = obj;
            var serialized = JsonConvert.SerializeObject(dic, GetSettingsForTest());

            Console.WriteLine(serialized);
            //Simulate another assembly
            var oldVersion = serialized.Replace("Jarvis.Framework.Tests, Version=1.0.0.0", "Altro.Assembly.Divertente, Version=0.9.0.0");
            //now change namespacve
            oldVersion = oldVersion.Replace(
                "Jarvis.Framework.Tests.BusTests.JarvisFrameworkRebusSerializationBinderTests+ClassWithArray",
                "Altro.Assembly.Divertente.Tests.Supercalifragilistichespiralidoso.JarvisFrameworkRebusSerializationBinderTests+ClassWithArray");

            Console.WriteLine(oldVersion);
            //Cannot survive deserialization with full information
            Assert.Throws<JsonSerializationException>(() => JsonConvert.DeserializeObject<Dictionary<String, ClassWithArray>>(oldVersion, GetSettingsForTest()));

            //we can survive update version only with default
            var deserialized = JsonConvert.DeserializeObject<Dictionary<String, ClassWithArray>>(oldVersion, GetDefaultSettings());
            Assert.That(deserialized["test"].DomainEvents, Is.EquivalentTo(new[] { "Test" }));
        }

        [Test]
        public void Immediate_deserialization() 
        {
            //This serialized string was generated using different assembly, namespace and version
            const string test = @"{""$type"":""System.Collections.Generic.Dictionary`2[[System.String, mscorlib, Version = 4.0.0.0, Culture = neutral, PublicKeyToken = b77a5c561934e089],[Altro.Assembly.Divertente.Tests.Supercalifragilistichespiralidoso.JarvisFrameworkRebusSerializationBinderTests+ClassWithArray, Altro.Assembly.Divertente, Version = 0.9.0.0, Culture = neutral, PublicKeyToken = null]], mscorlib, Version = 4.0.0.0, Culture = neutral, PublicKeyToken = b77a5c561934e089"",""test"":{""$type"":""Altro.Assembly.Divertente.Tests.Supercalifragilistichespiralidoso.JarvisFrameworkRebusSerializationBinderTests + ClassWithArray, Altro.Assembly.Divertente, Version = 0.9.0.0, Culture = neutral, PublicKeyToken = null"",""DomainEvents"":{""$type"":""System.String[], mscorlib, Version = 4.0.0.0, Culture = neutral, PublicKeyToken = b77a5c561934e089"",""$values"":[""Test""]}}}";
            var deserialized = JsonConvert.DeserializeObject<Dictionary<String, ClassWithArray>>(test);
            Assert.That(deserialized["test"].DomainEvents, Is.EquivalentTo(new[] { "Test" }));
        }

        [Test]
        public void Cannot_survive_changing_name_of_final_class()
        {
            Dictionary<String, ClassWithArray> dic = new Dictionary<string, ClassWithArray>();
            var obj = new ClassWithArray();
            obj.DomainEvents = new[] { "Test" };
            dic["test"] = obj;
            var serialized = JsonConvert.SerializeObject(dic, GetSettingsForTest());

            Console.WriteLine(serialized);
            //Simulate that the serialization was done by an older version of the framework.
            var oldVersion = serialized.Replace(
                "Jarvis.Framework.Tests.BusTests.JarvisFrameworkRebusSerializationBinderTests+ClassWithArray",
                "This.is.A.Complete.Mess.Of.Type.Name");

            Console.WriteLine(oldVersion);
            Assert.Throws<JsonSerializationException>(() => JsonConvert.DeserializeObject<Dictionary<String, ClassWithArray>>(oldVersion, GetSettingsForTest()));
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
