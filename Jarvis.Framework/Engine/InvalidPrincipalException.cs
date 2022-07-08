using System;

namespace Jarvis.Framework.Kernel.Engine
{
    [Serializable]
    public class InvalidPrincipalException : Exception
    {
        public InvalidPrincipalException() : base()
        {
        }

        public InvalidPrincipalException(string message) : base(message)
        {
        }

        public InvalidPrincipalException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected InvalidPrincipalException(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context) : base(info, context)
        {
        }
    }
}