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
			var method = state.GetType().Method("When", new[] { payload.GetType() }, Flags.InstanceAnyVisibility);
			if (method != null)
				method.Call(state, new object[] { payload });

			return null;
		}
	}
}
