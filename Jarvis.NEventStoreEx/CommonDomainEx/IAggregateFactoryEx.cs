using NStore.Aggregates;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jarvis.NEventStoreEx.CommonDomainEx
{
    /// <summary>
    /// This is needed because we want to resolve aggregate 
    /// with castle if they are registered with castle.
    /// </summary>
    public interface IAggregateFactoryEx
    {
        /// <summary>
        /// Build an aggregate given the id.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="id"></param>
        /// <returns></returns>
        IAggregate Build(Type type, IIdentity id);
    }
}
