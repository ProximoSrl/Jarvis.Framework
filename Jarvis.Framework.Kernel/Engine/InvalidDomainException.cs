using Jarvis.NEventStoreEx.CommonDomainEx;
using Jarvis.NEventStoreEx.CommonDomainEx.Core;

namespace Jarvis.Framework.Kernel.Engine
{
    public class InvalidDomainException : DomainException
    {
        public InvalidDomainException(AggregateRoot aggregate)
            : base(aggregate.Id.ToString(), string.Format("{0} {1} is invalid", aggregate.GetType().FullName, aggregate.Id))
        {
        }
    }
}