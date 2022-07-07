using Jarvis.Framework.Rebus.Support;
using NUnit.Framework;
using Rebus.Messages;
using Rebus.Serialization;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Jarvis.Framework.Tests.BusTests
{
    [TestFixture]
    [Category("Serialization")]
    public class JarvisFrameworkRebusJsonSerializerTests
    {
        [Test]
        public async Task Resilient_to_Change_Version()
        {
            await TestBody(s => s.Replace(
                "Jarvis.Framework.Tests, Version=1.0.0.0",
                "Jarvis.Framework.Tests, Version=0.4.0.0")).ConfigureAwait(false);
        }

        [Test]
        public async Task Resilient_to_Change_Assembly()
        {
            await TestBody(s => s.Replace(
                "Jarvis.Framework.Tests, Version=1.0.0.0",
                "Jarvis.Framework.OldAssembly, Version=1.0.0.0")).ConfigureAwait(false);
        }

        private async Task TestBody(Func<string, string> stringManipulation)
        {
            var sut = CreateSut();
            Dictionary<String, TestClass> dic = new Dictionary<string, TestClass>();
            var obj = new TestClass();
            obj.DomainEvents = new[] { "Test" };
            dic["test"] = obj;
            var message = new Message(new Dictionary<String, String>(), dic);
            var serialized = await sut.Serialize(message).ConfigureAwait(false);

            var stringSerialized = Encoding.UTF8.GetString(serialized.Body);
            Console.WriteLine(stringSerialized);

            var simulatedOldString = stringManipulation(stringSerialized);

            var simulatedOldMessage = new TransportMessage(serialized.Headers, Encoding.UTF8.GetBytes(simulatedOldString));

            var deserialized = await sut.Deserialize(simulatedOldMessage).ConfigureAwait(false);
            Assert.That(deserialized.Body, Is.InstanceOf<Dictionary<String, TestClass>>());

            var deserializedObject = (Dictionary<String, TestClass>)deserialized.Body;
            Assert.That(deserializedObject["test"].DomainEvents, Is.EquivalentTo(new[] { "Test" }));
        }

        JarvisFrameworkRebusJsonSerializer CreateSut()
        {
            return new JarvisFrameworkRebusJsonSerializer(
                new SimpleAssemblyQualifiedMessageTypeNameConvention(),
                BusBootstrapper.JsonSerializerSettingsForRebus);
        }

        public class TestClass
        {
            public String[] DomainEvents { get; set; }
        }

        class SimpleAssemblyQualifiedMessageTypeNameConvention : IMessageTypeNameConvention
        {
            public string GetTypeName(Type type) => type.AssemblyQualifiedName;

            public Type GetType(string name) => Type.GetType(name);
        }
    }
}
