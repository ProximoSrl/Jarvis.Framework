using Jarvis.NEventStoreEx.CommonDomainEx.Core;

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