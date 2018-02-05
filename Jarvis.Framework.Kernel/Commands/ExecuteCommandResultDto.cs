using Jarvis.Framework.Shared.Messages;
using Jarvis.Framework.Shared.ReadModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jarvis.Framework.Kernel.Commands
{
    /// <summary>
    /// Represent a dto containing the result of a command that was executed
    /// by a service (bounded context). It contains the list of Conflicting
    /// commands if the caller ask for execution of a command that was executed
    /// originally offline.
    /// </summary>
    public class ExecuteCommandResultDto
    {
        public ExecuteCommandResultDto(Boolean success, string error, Exception exception)
        {
            Success = success;
            Error = error;
            OriginalException = exception;
        }

        public Boolean Success { get; private set; }

        public String Error { get; private set; }

        public Exception OriginalException { get; set; }

        /// <summary>
        /// if the command is executed with an <see cref="MessagesConstants.IfVersionEqualsTo"/>
        /// and the aggregate is changed, this collection will contains list of the conflicting
        /// commands.
        /// </summary>
        public List<ConflictingCommandInfo> ConflictingCommands { get; set; }
    }
}
