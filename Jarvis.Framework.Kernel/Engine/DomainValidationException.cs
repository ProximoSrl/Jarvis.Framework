using Jarvis.NEventStoreEx.CommonDomainEx;
using Jarvis.NEventStoreEx.CommonDomainEx.Core;

namespace Jarvis.Framework.Kernel.Engine
{
    public class DomainValidationException : DomainException
    {
        public DomainValidationException(AggregateRoot aggregate, string message)
            : base(aggregate.Id.ToString(), message)
        {
        }
    }
}