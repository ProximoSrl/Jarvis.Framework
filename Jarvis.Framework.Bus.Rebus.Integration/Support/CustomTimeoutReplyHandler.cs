//using System;
//using Jarvis.Framework.Shared.Messages;
//using Newtonsoft.Json;
//using Rebus;
//using Rebus.Logging;
//using Rebus.Messages;
//using Rebus.Handlers;
//using Rebus.Bus;

//namespace Jarvis.Framework.Bus.Rebus.Integration.Support
//{
//    public class CustomTimeoutReplyHandler : IHandleMessages<TimeoutReply>
//    {
//        const string TimeoutReplySecretCorrelationId = "rebus.secret.deferred.message.id";

//        static readonly JsonSerializerSettings JsonSerializerSettings =
//            new JsonSerializerSettings
//            {
//                TypeNameHandling = TypeNameHandling.All,
//                ContractResolver = new MessagesContractResolver(),
//                ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor
//            };

//        static ILog log;
//        readonly IBus bus;

//        static CustomTimeoutReplyHandler()
//        {
//            RebusLoggerFactory.Changed += f => log = f.GetCurrentClassLogger();
//        }

//        public CustomTimeoutReplyHandler(IBus bus)
//        {
//            this.bus = bus;
//        }

//        public void Handle(TimeoutReply message)
//        {
//            if (message.CorrelationId != TimeoutReplySecretCorrelationId)
//                return;

//            var deferredMessages = Deserialize(message.CustomData);

//            log.Info("CQRS Custom handler received timeout reply - sending deferred message to self.");

//            foreach (var dm in deferredMessages.Messages)
//            {
//                Dispatch(dm, message.SagaId);
//            }
//        }

//        private void Dispatch(object deferredMessage, Guid sagaId)
//        {
//            if (sagaId != Guid.Empty)
//            {
//                bus.AttachHeader(deferredMessage, Headers.AutoCorrelationSagaId, sagaId.ToString());
//            }

//            bus.Send(deferredMessage);
//        }

//        Message Deserialize(string customData)
//        {
//            return (Message)JsonConvert.DeserializeObject(customData, JsonSerializerSettings);
//        }
//    }
//}