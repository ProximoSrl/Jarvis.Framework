using System;
using Jarvis.Framework.Shared.Commands;

namespace Jarvis.Framework.Tests.BusTests.MessageFolder
{
    public class AnotherSampleTestCommand : Command
    {
        public AnotherSampleTestCommand(int id, String description)
        {
            Id = id;
            Description = description;
        }

        public int Id { get; private set; }

        public String Description { get; set; }

        public override string Describe()
        {
            return $"AnotherSampleTestCommand Id:{Id} MessageId: {MessageId} Description: {Description}";
        }
    }
}