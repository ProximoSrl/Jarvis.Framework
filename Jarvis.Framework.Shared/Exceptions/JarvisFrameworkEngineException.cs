using System;

namespace Jarvis.Framework.Shared.Exceptions
{
    [Serializable]
    public class JarvisFrameworkEngineException : Exception
    {
        public JarvisFrameworkEngineException() : base()
        {
        }

        public JarvisFrameworkEngineException(string message) : base(message)
        {
        }

        public JarvisFrameworkEngineException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected JarvisFrameworkEngineException(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context) : base(info, context)
        {
        }
    }
}
