using Jarvis.Framework.Shared.Messages;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

    public class SagaDeferredMessage : IMessage 
    {
        public IMessage MessageToDispatch { get; set; }

        public DateTime TimeToDispatch { get; set; }

        public SagaDeferredMessage(
            IMessage messageToDispatch,
            DateTime timeToDispatch)
        {
            MessageToDispatch = messageToDispatch;
            TimeToDispatch = timeToDispatch;
            MessageId = Guid.NewGuid();
        }

        public Guid MessageId { get; set; }
      
        public string Describe()
        {
            return String.Format("Deferred Saga Message. Time {0}, original message: {1} ", TimeToDispatch, MessageToDispatch);
        }
    }

    public class SagaTimeout : SagaMessage
    {
        public string TimeOutKey { get;private  set; }

        public SagaTimeout(String sagaId, string timeOutKey = null) 
            : base(sagaId)
        {
            TimeOutKey = timeOutKey;
        }
    }
}
