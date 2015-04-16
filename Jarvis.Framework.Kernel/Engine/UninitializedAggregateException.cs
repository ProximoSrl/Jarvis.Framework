using System;
using Jarvis.NEventStoreEx.CommonDomainEx;
using Jarvis.NEventStoreEx.CommonDomainEx.Core;

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
