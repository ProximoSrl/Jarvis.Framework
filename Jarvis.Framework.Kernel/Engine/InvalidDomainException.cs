using Jarvis.Framework.Shared.Exceptions;
using NStore.Domain;

namespace Jarvis.Framework.Kernel.Engine
{
    public class InvalidDomainException : DomainException
    {
        public InvalidDomainException(IAggregate aggregate)
            : base(aggregate.Id, string.Format("{0} {1} is invalid", aggregate.GetType().FullName, aggregate.Id))
        {
        }
    }
}