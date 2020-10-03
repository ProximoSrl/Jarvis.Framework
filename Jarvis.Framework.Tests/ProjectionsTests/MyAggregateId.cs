using Jarvis.Framework.Shared.IdentitySupport;

namespace Jarvis.Framework.Tests.ProjectionsTests
{
    public class MyAggregateId : EventStoreIdentity
    {
        public MyAggregateId(long id)
            : base(id)
        {
        }

        public MyAggregateId(string id)
            : base(id)
        {
        }
    }
}