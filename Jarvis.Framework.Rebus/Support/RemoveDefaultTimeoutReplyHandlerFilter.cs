//using System;
//using System.Collections.Generic;
//using System.Linq;
//using Rebus;
//using Rebus.Messages;

//namespace Jarvis.Framework.Bus.Rebus.Integration.Support
//{
//    public class RemoveDefaultTimeoutReplyHandlerFilter : IInspectHandlerPipeline
//    {
//        public IEnumerable<IHandleMessages> Filter(object message, IEnumerable<IHandleMessages> handlers)
//        {
//            if (!(message is TimeoutReply))
//                return handlers;

//            var hlist = handlers.ToList();
//            int removed = hlist.RemoveAll(x => x.GetType().FullName == "Rebus.Bus.TimeoutReplyHandler");
//            if (removed != 1)
//                throw new Exception("Something changed in Rebus sources. Check!");

//            return hlist;
//        }
//    }
//}