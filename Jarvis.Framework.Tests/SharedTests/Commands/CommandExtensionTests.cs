using Jarvis.Framework.Shared.Helpers;
using Jarvis.Framework.Tests.BusTests.MessageFolder;
using Newtonsoft.Json;
using NUnit.Framework;
using System;

namespace Jarvis.Framework.Tests.SharedTests.Commands
{
    [TestFixture]
    public class CommandExtensionTests
    {
        private const string StringValue = "Hello Header";

        [Test]
        public void Test_extensions_write_object_to_command_Context_dateTime()
        {
            var cmd = new SampleTestCommand(1);
            var data = DateTime.Now;
            cmd.SetContextData("data", data);
            Assert.That(cmd.GetContextData<DateTime>("data"), Is.EqualTo(data));
        }

        [Test]
        public void Test_extensions_write_object_to_command_Context_dateTime_from_string()
        {
            var cmd = new SampleTestCommand(1);
            var data = DateTime.Now;
            cmd.SetContextData("data", JsonConvert.SerializeObject(data, CommandExtensions.JsonSerializerSettings));
            Assert.That(cmd.GetContextData<DateTime>("data"), Is.EqualTo(data));
        }

        [Test]
        public void Test_extensions_write_object_to_command_Context_dateTime_nullable()
        {
            var cmd = new SampleTestCommand(1);
            var data = DateTime.Now;
            cmd.SetContextData("data", data);
            Assert.That(cmd.GetContextData<DateTime?>("data"), Is.EqualTo(data));
        }

        [Test]
        public void Test_extensions_write_object_to_command_Context_int()
        {
            var cmd = new SampleTestCommand(1);
            cmd.SetContextData("data", 42);
            Assert.That(cmd.GetContextData<int>("data"), Is.EqualTo(42));
        }

        [Test]
        public void Test_extensions_write_object_to_command_Context_mismatch_throw()
        {
            var cmd = new SampleTestCommand(1);
            cmd.SetContextData("data", 42);

            Assert.Throws<InvalidCastException>(() => cmd.GetContextData<DateTime>("data"));
        }

        [Test]
        public void Test_extensions_datetime_object()
        {
            var cmd = new SampleTestCommand(1);
            var data = new TestObject(StringValue, 42);
            cmd.SetContextData("obj", data);
            Assert.That(cmd.GetContextData<TestObject>("obj").Value, Is.EqualTo(StringValue));
            Assert.That(cmd.GetContextData<TestObject>("obj").Age, Is.EqualTo(42));
        }

        public class TestObject
        {
            public TestObject(string value, int age)
            {
                Value = value;
                Age = age;
            }

            public string Value { get; private set; }

            public int Age { get; private set; }
        }
    }
}
