using Jarvis.Framework.Shared.Exceptions;
using System;

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
