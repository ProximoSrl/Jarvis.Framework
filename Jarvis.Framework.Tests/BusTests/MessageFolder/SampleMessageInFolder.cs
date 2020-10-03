using System;
using Jarvis.Framework.Shared.Messages;

namespace Jarvis.Framework.Tests.BusTests.MessageFolder
{
    public class SampleMessageInFolder : IMessage
    {
        public SampleMessageInFolder(int id)
        {
            Id = id;
        }

        public int Id { get; private set; }
        public Guid MessageId { get { return Guid.Parse("18ABD708-90C6-417C-87F0-4E317868E331"); } }
        public string Describe()
        {
            return "SampleMessageInFolder";
        }
    }
}