using Fasterflect;
using NStore.Core.Processing;

namespace Jarvis.Framework.Kernel.Engine
{
	public sealed class AggregateStateEventPayloadProcessor : IPayloadProcessor
	{
		public static readonly IPayloadProcessor Instance = new AggregateStateEventPayloadProcessor();

		private AggregateStateEventPayloadProcessor()
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
            var method = state.GetType().Method("When", new[] { payload.GetType() }, Flags.InstanceAnyVisibility);
            if (method != null)
            {
                method.Call(state, new object[] { payload });
            }
        }
    }
}
