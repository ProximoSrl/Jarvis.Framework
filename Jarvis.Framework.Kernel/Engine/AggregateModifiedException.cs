using Jarvis.Framework.Shared.Messages;
using Jarvis.NEventStoreEx.CommonDomainEx;
using Jarvis.NEventStoreEx.CommonDomainEx.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Jarvis.Framework.Kernel.Engine
{
    /// <summary>
    /// Exception raised when a command is executed with
    /// <see cref="MessagesConstants.IfVersionEqualsTo"/> header active
    /// and the aggregate is newer than requested version.
    /// </summary>
    [Serializable]
    public class AggregateModifiedException : DomainException
    {
        
        public Int32 VersionRequested { get; private set; }

        public Int32 ActualVersion { get; private set; }

        public AggregateModifiedException(string message, String aggregateId, Int32 versionRequested, Int32 actualVersion) : base(aggregateId, message)
        {
            VersionRequested = versionRequested;
            ActualVersion = actualVersion;
        }

        protected AggregateModifiedException(String aggregateId, SerializationInfo info, StreamingContext context) : base(aggregateId, info, context)
        {
        }
    }
}
