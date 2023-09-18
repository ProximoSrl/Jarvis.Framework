using Fasterflect;
using NStore.Core.Processing;

namespace Jarvis.Framework.Kernel.Engine
{
    public sealed class AggregateStateFastEventPayloadProcessor : IPayloadProcessor
    {
        public static readonly IPayloadProcessor Instance = new AggregateStateFastEventPayloadProcessor();

        private AggregateStateFastEventPayloadProcessor()
        {
        }

        public object Process(object state, object payload)
        {
            InnerApplyPayload(state, payload);

            //now we need to check if state has aggregate child.
            var aggregateState = state as JarvisAggregateState;
            if (aggregateState?.EntityStates?.Count > 0)
            {
                foreach (var entityState in aggregateState.EntityStates)
                {
                    InnerApplyPayload(entityState.Value, payload);
                }
            }

            return null;
        }

        private static void InnerApplyPayload(object state, object payload)
        {
            state.CallMethod("When", new[] { payload.GetType() });
        }
    }
}
