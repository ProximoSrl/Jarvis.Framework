using System;
using System.Reflection;

namespace Jarvis.Framework.Shared.IdentitySupport
{
    /// <summary>
    /// Helps correlating identities to aggregates.
    /// </summary>
    public interface IIdentityToAggregateManager
    {
        /// <summary>
        /// Given an assembly with aggregates, it will scan for every aggregate
        /// and it will correlate to an identity.
        /// </summary>
        /// <param name="assemblyWithAggregates"></param>
        void ScanAssemblyForAggregateRoots(Assembly assemblyWithAggregates);

        /// <summary>
        /// Given an <see cref="EventStoreIdentity"/> this method will return the type
        /// of the corresponding aggregate.
        /// </summary>
        /// <param name="identity"></param>
        /// <returns></returns>
        Type GetAggregateFromId(EventStoreIdentity identity);
    }
}
