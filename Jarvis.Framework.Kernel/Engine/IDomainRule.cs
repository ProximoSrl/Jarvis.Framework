using Jarvis.NEventStoreEx.CommonDomainEx;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jarvis.Framework.Kernel.Engine
{
    /// <summary>
    /// Represents a rule of the domain that spans multiple aggregate. An aggregate
    /// can have a list of rules injected, and they are validated before repository
    /// save the aggregate.
    /// This is useful to implement rules that spans between multiple aggregates or
    /// somewhat depends from data outside the state of the aggregate.
    /// </summary>
    public interface IDomainRule<T> where T : AggregateState, new()
    {
        /// <summary>
        /// Validate the state of the object, if validation failed it throws a
        /// <see cref="DomainException"/>
        /// </summary>
        /// <param name="state"></param>
        /// <param name="id">Id of the aggregate root</param>
        void Validate(IIdentity id, T state);
    }
}
