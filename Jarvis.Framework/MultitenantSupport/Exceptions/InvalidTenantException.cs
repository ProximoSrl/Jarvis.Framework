using System;

namespace Jarvis.Framework.Kernel.MultitenantSupport.Exceptions
{
    [Serializable]
    public class InvalidTenantException : Exception
    {
        public InvalidTenantException() : base()
        {
        }

        public InvalidTenantException(string message) : base(message)
        {
        }

        public InvalidTenantException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected InvalidTenantException(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context) : base(info, context)
        {
        }
    }
}