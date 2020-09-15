using Jarvis.Framework.Tests.EngineTests;

namespace Jarvis.Framework.Tests.BusTests.MessageFolder
{
    public class SampleAggregateTestCommand : Command<SampleAggregateId>
    {
        public SampleAggregateTestCommand(SampleAggregateId aggregateId) : base(aggregateId)
        {
        }
    }
}
