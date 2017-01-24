using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Jarvis.Framework.Shared.Messages;
using System.Runtime.Serialization;
using Jarvis.NEventStoreEx.CommonDomainEx.Core;

namespace Jarvis.Framework.Kernel.Engine
{
    /// <summary>
    /// This is the exception raised when a command synced from offline
    /// mode and violates the checkpoint rule <see cref="MessagesConstants.SessionStartCheckEnabled"/>
    /// </summary>
    [Serializable]
    public class AggregateSyncConflictException : DomainException
    {
        public Int64 CheckpointToken { get; private set; }

        public String SessionGuidId { get; private set; }

        public AggregateSyncConflictException(
            string message, 
            String aggregateId,
            Int64 checkpointToken, 
            String sessionGuidId) : base(aggregateId, message)
        {
            CheckpointToken = checkpointToken;
            SessionGuidId = sessionGuidId;
        }

        protected AggregateSyncConflictException(String aggregateId, SerializationInfo info, StreamingContext context) : base(aggregateId, info, context)
        {
        }
    }
}
