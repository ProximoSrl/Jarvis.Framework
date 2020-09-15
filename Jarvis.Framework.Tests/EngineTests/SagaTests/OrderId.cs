namespace Jarvis.Framework.Tests.EngineTests.SagaTests
{
    public class OrderId : EventStoreIdentity
    {
        public OrderId(long id) : base(id)
        {
        }

        [JsonConstructor]
        public OrderId(string id) : base(id)
        {
        }
    }
}