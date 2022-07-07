﻿using System;

namespace Jarvis.Framework.Kernel.ProjectionEngine
{
    [Serializable]
    public class CollectionWrapperException : Exception
    {
        public CollectionWrapperException() : base()
        {
        }

        public CollectionWrapperException(string message) : base(message)
        {
        }

        public CollectionWrapperException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected CollectionWrapperException(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context) : base(info, context)
        {
        }
    }
}
