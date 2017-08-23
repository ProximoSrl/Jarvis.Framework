using Jarvis.Framework.Shared.Exceptions;

namespace Jarvis.Framework.Kernel.Engine
{
    public class InvalidAggregateIdException : DomainException
    {
        public InvalidAggregateIdException()
            : base("Identificativo non valido")
        {
        }
    }
}