using Castle.Core.Logging;
using Jarvis.Framework.Kernel.Engine;
using Jarvis.Framework.Shared.Exceptions;
using Jarvis.Framework.Shared.IdentitySupport;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Jarvis.Framework.Kernel.Support
{
    /// <summary>
    /// Default IdentityToAggregateManager to associate aggregate by id convention.
    /// </summary>
	public class IdentityToAggregateManager : IIdentityToAggregateManager
    {
        public IdentityToAggregateManager()
        {
            _aggregateRoots = new List<Type>();
            _associationCache = new Dictionary<Type, Type>();
            Logger = NullLogger.Instance;
        }

        public ILogger Logger { get; set; }

        private readonly List<Type> _aggregateRoots;
        private readonly Dictionary<Type, Type> _associationCache;

        public Type GetAggregateFromId(EventStoreIdentity identity)
        {
            Type type;
            if (_associationCache.TryGetValue(identity.GetType(), out type))
            {
                return type;
            }

            var tag = identity.GetTag();
            var namespaceFirstPart = identity.GetType().Namespace.Split('.').First();
            var aggregate = _aggregateRoots
                .Where(_ => _.Namespace.StartsWith(namespaceFirstPart + ".")
                    && _.Name.Equals(tag, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (aggregate.Count > 1)
            {
                Logger.ErrorFormat("Unable to find related aggregate for id {0}, we have multiple aggregate with naming convention {1}", identity.GetType().FullName, String.Join(",", aggregate.Select(_ => _.FullName)));
                throw new JarvisFrameworkEngineException($"Unable to find related aggregate for id {identity.GetType().FullName}, we have multiple aggregate with naming convention {aggregate.Select(_ => _.FullName)}");
            }

            return aggregate.FirstOrDefault();
        }

        private HashSet<Assembly> _scannedAssemblies = new HashSet<Assembly>();

        /// <summary>
        /// This scans for all types that inherits from AggregateRoot. This is needed to build a 
        /// cache that associates the type of the aggregate to the type of the id, so we can 
        /// correctly find the type of the aggregate given the id.
        /// </summary>
        /// <param name="assemblyWithAggregates"></param>
        public void ScanAssemblyForAggregateRoots(Assembly assemblyWithAggregates)
        {
            if (_scannedAssemblies.Contains(assemblyWithAggregates))
            {
                return;
            }

            var sb = new StringBuilder();
            Type[] aggregateTypes = assemblyWithAggregates.GetTypes();
            var aggregateEntities = aggregateTypes
                .Where(_ => !_.IsAbstract && _.IsClass)
                .Where(_ => _.BaseType.IsGenericType && _.BaseType.GetGenericTypeDefinition() == typeof(AggregateRoot<,>));

            foreach (var aggregate in aggregateEntities)
            {
                _aggregateRoots.Add(aggregate);
                var typeId = aggregate.BaseType.GetGenericArguments()[1];
                if (_associationCache.ContainsKey(typeId))
                {
                    throw new JarvisFrameworkEngineException($"Aggregate {aggregate.FullName} declared an identity of type {typeId} already associated to aggregate {_associationCache[typeId]}");
                }
                _associationCache.Add(typeId, aggregate);
            }

            var errors = sb.ToString();
            if (!String.IsNullOrWhiteSpace(errors))
                throw new JarvisFrameworkEngineException("Found identities with errors:\n" + errors);

            _scannedAssemblies.Add(assemblyWithAggregates);
        }
    }
}
