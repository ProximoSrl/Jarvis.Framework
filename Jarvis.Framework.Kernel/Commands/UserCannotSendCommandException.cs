using System;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading;
using Castle.Core.Logging;
using Castle.MicroKernel;
using NEventStore.Domain.Persistence;
using Jarvis.Framework.Kernel.Engine;
using Jarvis.Framework.Kernel.Store;
using Jarvis.Framework.Kernel.Support;
using Jarvis.Framework.Shared.Commands;
using Jarvis.Framework.Shared.MultitenantSupport;
using Newtonsoft.Json;
using Jarvis.Framework.Shared.ReadModel;
using Jarvis.Framework.Shared.Messages;
using Jarvis.Framework.Shared.Logging;
using Jarvis.NEventStoreEx.CommonDomainEx.Core;

namespace Jarvis.Framework.Kernel.Commands
{
    /// <summary>
    /// Exception raised when a user cannot send a command.
    /// </summary>
    public class UserCannotSendCommandException : Exception
    {
        public UserCannotSendCommandException() : base()
        {
        }

        public UserCannotSendCommandException(string message) : base(message)
        {
        }

        public UserCannotSendCommandException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected UserCannotSendCommandException(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context) : base(info, context)
        {
        }
    }
}
