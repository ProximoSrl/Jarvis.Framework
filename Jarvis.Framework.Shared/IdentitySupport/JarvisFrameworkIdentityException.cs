using System;

namespace Jarvis.Framework.Shared.IdentitySupport
{
    /// <summary>
    /// Generic exception in identity managament
    /// </summary>
    [Serializable]
    public class JarvisFrameworkIdentityException : Exception
    {
        public JarvisFrameworkIdentityException() : base()
        {
        }

        public JarvisFrameworkIdentityException(string message) : base(message)
        {
        }

        public JarvisFrameworkIdentityException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected JarvisFrameworkIdentityException(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context) : base(info, context)
        {
        }
    }
}
