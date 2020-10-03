using System;

namespace Jarvis.Framework.Kernel.Engine
{
	[Serializable]
    public class MissingDefaultCtorException : Exception
    {
        public MissingDefaultCtorException(Type t)
            : base(string.Format("Type {0} has no default constructor. Add protected {1}(){{}}", t.FullName, t.Name))
        {
        }

        public MissingDefaultCtorException() : base()
        {
        }

        public MissingDefaultCtorException(string message) : base(message)
        {
        }

        public MissingDefaultCtorException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected MissingDefaultCtorException(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context) : base(info, context)
        {
        }
    }
}
