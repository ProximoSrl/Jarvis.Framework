using System;
using Jarvis.Framework.Shared.Exceptions;

namespace Jarvis.Framework.Kernel.Engine
{
    [Serializable]
    public class UninitializedAggregateException : DomainException
    {
        public UninitializedAggregateException()
            : base("Aggregate has no events!")
        {
        }
    }
}
