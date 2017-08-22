using System;
using System.Threading;
using Jarvis.Framework.Kernel.Events;
using Jarvis.Framework.Kernel.MultitenantSupport;
using Jarvis.Framework.Shared.IdentitySupport;
using Jarvis.Framework.Shared.MultitenantSupport;
using Jarvis.Framework.Shared.Helpers;
using System.Threading.Tasks;

namespace Jarvis.Framework.Tests.EngineTests
{
    public class TenantATestSettings : TenantSettings
    {
        public TenantATestSettings() : base(new TenantId("a"))
        {
        }

        protected override ICounterService CreateCounterService()
        {
            return new InMemoryCounterService();
        }
    } 
    
    public class TenantBTestSettings : TenantSettings
    {
        public TenantBTestSettings()
            : base(new TenantId("b"))
        {
        }

        protected override ICounterService CreateCounterService()
        {
            return new InMemoryCounterService();
        }
    }

    public class SempleAggregteCreatedEventHandler : IEventHandler<SampleAggregateCreated>
    {
        public int CallCounter = 0;

        public Task On(SampleAggregateCreated e)
        {
            CallCounter++;
            return TaskHelpers.CompletedTask;
        }
    }

    public class UnsafeHandler : IEventHandler<SampleAggregateCreated>
    {
        public static long Counter = 0;

        public Task On(SampleAggregateCreated e)
        {
            var value = Counter;
            var random = new Random(DateTime.UtcNow.Millisecond);
            Thread.Sleep(random.Next(100));
            Counter = value + 1;
            return TaskHelpers.CompletedTask;
        }
    }
}
