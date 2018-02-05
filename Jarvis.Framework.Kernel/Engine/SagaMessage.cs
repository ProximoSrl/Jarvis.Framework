using Jarvis.Framework.Shared.Messages;
using Newtonsoft.Json;
using System;

namespace Jarvis.Framework.Kernel.Engine
{
	public class SagaMessage : IMessage
	{
		/// <summary>
		/// identificativo dell'evento
		/// </summary>
		public Guid MessageId { get; private set; }

		public String SagaId { get; private set; }

		public virtual string Describe()
		{
			return JsonConvert.SerializeObject(this, Formatting.Indented);
		}

		public SagaMessage(String sagaId)
		{
			MessageId = Guid.NewGuid();
			SagaId = sagaId;
		}
	}

	public class SagaTimeout : SagaMessage
	{
		public string TimeOutKey { get; private set; }

		public SagaTimeout(String sagaId, string timeOutKey = null)
			: base(sagaId)
		{
			TimeOutKey = timeOutKey;
		}
	}
}
