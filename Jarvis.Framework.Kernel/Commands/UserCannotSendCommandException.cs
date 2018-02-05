using System;
namespace Jarvis.Framework.Kernel.Commands
{
    /// <summary>
    /// Exception raised when a user cannot send a command.
    /// </summary>
    [Serializable]
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
