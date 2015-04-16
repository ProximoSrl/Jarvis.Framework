using Jarvis.NEventStoreEx.CommonDomainEx;
using Jarvis.NEventStoreEx.CommonDomainEx.Core;

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