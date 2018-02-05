using Jarvis.Framework.Shared.Commands;

namespace Jarvis.Framework.Tests.BusTests.MessageFolder
{
    public class SampleTestCommand : Command
    {
        public SampleTestCommand(int id)
        {
            Id = id;
        }

        public int Id { get; private set; }

        public override string Describe()
        {
            return "SampleTestCommand description";
        }
    }
}