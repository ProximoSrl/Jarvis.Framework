using Jarvis.Framework.Shared.Exceptions;

namespace Jarvis.Framework.Kernel.Engine
{
    public class InvalidAggregateOperationException : DomainException
    {
        public InvalidAggregateOperationException(string message)
            : base(message)
        {
        }
    }
}