using Castle.Core.Logging;
using Jarvis.Framework.Bus.Rebus.Integration.Support;
using Jarvis.Framework.Shared.Domain.Serialization;
using Jarvis.Framework.Shared.Events;
using Jarvis.Framework.Shared.Exceptions;
using Jarvis.Framework.Shared.Messages;
using Newtonsoft.Json;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using static Jarvis.Framework.Tests.DomainTests.DomainEventIdentityBsonSerializationTests;

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

        private JsonSerializerSettings GetDefaultSettingsWithFullSerialization() => new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.All,
            TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Full,
            ContractResolver = new MessagesContractResolver(),
            ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor,
            Converters = new JsonConverter[]
            {
                new StringValueJsonConverter()
            }
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
        public void Array_with_generic()
        {
            var obj = new ClassGeneric<String, int>[] { new ClassGeneric<String, int>
                {
                    PropT = "Ciao",
                    PropU = 42
                }
            };
            var serialized = JsonConvert.SerializeObject(obj, GetSettingsForTest());
            Console.WriteLine(serialized);

            var defaultSerialized = JsonConvert.SerializeObject(obj, GetDefaultSettingsWithFullSerialization());
            Console.WriteLine(defaultSerialized);

            var deserialized = JsonConvert.DeserializeObject<ClassGeneric<String, int>[]>(serialized, GetSettingsForTest());

            Assert.That(deserialized.Single().PropT, Is.EqualTo("Ciao"));
        }

        [Test]
        public void Class_generic_with_Array()
        {
            var obj = new ClassWithArrayGeneric();
            obj.DomainEvents = new[] { new ClassGeneric<String, int> 
                {
                    PropT = "Ciao",
                    PropU = 42
                } 
            };
            var serialized = JsonConvert.SerializeObject(obj, GetSettingsForTest());
            Console.WriteLine(serialized);

            var defaultSerialized = JsonConvert.SerializeObject(obj, GetDefaultSettingsWithFullSerialization());
            Console.WriteLine(defaultSerialized);

            var deserialized = JsonConvert.DeserializeObject< ClassWithArrayGeneric>(serialized, GetSettingsForTest());

            Assert.That(deserialized.DomainEvents.Single().PropT, Is.EqualTo("Ciao"));
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
            Assert.That(deserialized["test"].DomainEvents, Is.EquivalentTo(new[] { "Test" }));
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
            var actualVersion = Assembly.GetExecutingAssembly().GetName().Version;
            var oldVersion = serialized.Replace($"Jarvis.Framework.Tests, Version={actualVersion}", $"Jarvis.Framework.OldAssembly, Version={actualVersion}");
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

        [Test]
        public void Generic_object_serialization()
        {
            ClassWithAnonymousType classWithAnonymousType = new ClassWithAnonymousType()
            {
                DomainEvents = new[] { "foo", "bar" },
                ExtraData = new SomeData()
                {
                    Property = "this is a property"
                }
            };
            var serialized = JsonConvert.SerializeObject(classWithAnonymousType, GetSettingsForTest());

            Console.WriteLine(serialized);

            var deserialized = JsonConvert.DeserializeObject<ClassWithAnonymousType>(serialized, GetSettingsForTest());
            Assert.That(deserialized.ExtraData, Is.InstanceOf<SomeData>());
            Assert.That(((SomeData)deserialized.ExtraData).Property, Is.EqualTo("this is a property"));
        }

        [Test]
        public void Generic_object_serialization_with_anonymous_types_does_not_throws()
        {
            ClassWithAnonymousType classWithAnonymousType = new ClassWithAnonymousType()
            {
                DomainEvents = new[] { "foo", "bar" },
                ExtraData = new
                {
                    Property = "this is a property"
                }
            };
            var serialized = JsonConvert.SerializeObject(classWithAnonymousType, GetSettingsForTest());
            var defaultSerialized = JsonConvert.SerializeObject(classWithAnonymousType, GetDefaultSettingsWithFullSerialization());

            Console.WriteLine(serialized);
            Console.WriteLine(defaultSerialized);

            Assert.DoesNotThrow(() => JsonConvert.DeserializeObject<ClassWithAnonymousType>(serialized, GetSettingsForTest()));
        }

        [Test]
        public void Generic_object_serialization_with_anonymous_of_anonymous_types_does_not_throws()
        {
            var changeset = new DomainEvent[]
            {
                new SampleEvent("ciao"),
                new SampleEvent("foo")
            };
            ClassWithAnonymousType classWithAnonymousType = new ClassWithAnonymousType()
            {
                DomainEvents = new[] { "foo", "bar" },
                ExtraData = new
                {
                    Events = changeset.Select(evt => new
                    {
                        evt.GetType().Name,
                        Data = evt
                    }).ToArray(),
                }
            };
            var serialized = JsonConvert.SerializeObject(classWithAnonymousType, GetSettingsForTest());
            var defaultSerialized = JsonConvert.SerializeObject(classWithAnonymousType, GetDefaultSettingsWithFullSerialization());

            Console.WriteLine(serialized);
            Console.WriteLine(defaultSerialized);

            ClassWithAnonymousType test = JsonConvert.DeserializeObject<ClassWithAnonymousType>(defaultSerialized, GetDefaultSettingsWithFullSerialization());

            Assert.DoesNotThrow(() => JsonConvert.DeserializeObject<ClassWithAnonymousType>(serialized, GetSettingsForTest()));
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

        public class ClassWithArrayGeneric
        {
            public ClassGeneric<string, int>[] DomainEvents { get; set; }
        }

        public class ClassGeneric<T, U> 
        {
            public U PropU { get; set; }

            public T PropT { get; set; }
        }

        public class ClassWithAnonymousType
        {
            public String[] DomainEvents { get; set; }

            public Object ExtraData { get; set; }
        }

        public class SomeData
        {
            public string Property { get; set; }
        }
    }
}
