using Jarvis.Framework.Shared.Exceptions;
using NStore.Domain;

namespace Jarvis.Framework.Kernel.Engine
{
    public class DomainValidationException : DomainException
    {
        public DomainValidationException(IAggregate aggregate, string message)
            : base(aggregate.Id, message)
        {
        }
    }
}