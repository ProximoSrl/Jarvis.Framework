using Castle.Core.Logging;
using Fasterflect;
using Jarvis.Framework.Shared.Exceptions;
using Jarvis.Framework.Shared.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Jarvis.Framework.Shared.IdentitySupport
{
    public class IdentityManager : IIdentityManager, IIdentityImportManager
    {
        private readonly ICounterService _counterService;

        private readonly Dictionary<string, Func<long, IIdentity>> _longBasedFactories =
            new Dictionary<string, Func<long, IIdentity>>(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, Type> _tagToTypeMapping =
            new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);

        public ILogger Logger { get; set; }

        public IdentityManager(ICounterService counterService)
        {
            _counterService = counterService;
            Logger = NullLogger.Instance;
        }

        public string ToString(IIdentity identity)
        {
            return identity.ToString();
        }

        public IIdentity ToIdentity(string identityAsString)
        {
            if (String.IsNullOrEmpty(identityAsString))
                return null;

            int pos = identityAsString.IndexOf(EventStoreIdentity.Separator);
            if (pos == -1)
            {
                throw new JarvisFrameworkIdentityException(string.Format("invalid identity value {0}", identityAsString));
            }

            var tag = identityAsString.Substring(0, pos);
            if (!long.TryParse(identityAsString.Substring(pos + 1), out var id))
            {
                throw new JarvisFrameworkIdentityException(string.Format("invalid identity value {0}", identityAsString));
            }

            if (id < 0)
            {
                throw new JarvisFrameworkIdentityException(string.Format("invalid identity value {0} - id value greater than long.maxvalue", identityAsString));
            }

            if (!_longBasedFactories.TryGetValue(tag, out Func<long, IIdentity> factory))
            {
                throw new JarvisFrameworkIdentityException(string.Format("{0} not registered in IdentityManager", tag));
            }
            return factory(id);
        }

        public TIdentity ToIdentity<TIdentity>(string identityAsString) where TIdentity : IIdentity
        {
            var identity = ToIdentity(identityAsString);
            if (identity is TIdentity typedIdentity)
            {
                return typedIdentity;
            }
            throw new JarvisFrameworkIdentityException(string.Format("Identity {0} is not of type {1}", identityAsString, typeof(TIdentity).FullName));
        }

        public void RegisterIdentitiesFromAssembly(Assembly assembly)
        {
            var sb = new StringBuilder();
            foreach (var ic in assembly.GetTypes().Where(x => typeof(IIdentity).IsAssignableFrom(x) && !x.IsAbstract && x.IsClass))
            {
                var tag = EventStoreIdentity.GetTagForIdentityClass(ic);

                {
                    var ctor = ic.Constructor(new Type[] { typeof(long) });
                    if (ctor == null)
                    {
                        var message = String.Format("Identity {0} must have the constructor {1}(long id)\n", ic.FullName, ic.Name);
                        Logger.Error(message);
                        sb.AppendFormat(message);
                        continue; //move to the next type, or everythign will crash.
                    }
                    var activator = FastReflectionHelper.GetActivator(ctor);

                    _longBasedFactories[tag] = (id) => (IIdentity)activator(new object[] { id });
                    _tagToTypeMapping[tag] = ic;
                    
                    // Also register by the full class name for convenience
                    // This allows lookup by both "Document" (tag) and "DocumentId" (class name)
                    var fullClassName = ic.Name;
                    if (!_tagToTypeMapping.ContainsKey(fullClassName))
                    {
                        _tagToTypeMapping[fullClassName] = ic;
                    }
                }
            }

            var errors = sb.ToString();
            if (!String.IsNullOrWhiteSpace(errors))
                throw new JarvisFrameworkEngineException("Found identities with errors:\n" + errors);
        }

        public TIdentity New<TIdentity>() where TIdentity : IIdentity
        {
            var tag = EventStoreIdentity.GetTagForIdentityClass(typeof(TIdentity));
            Func<long, IIdentity> factory = GetFactoryForTag(tag);

            return (TIdentity)factory(_counterService.GetNext(tag));
        }

        public async Task<TIdentity> NewAsync<TIdentity>() where TIdentity : IIdentity
        {
            var tag = EventStoreIdentity.GetTagForIdentityClass(typeof(TIdentity));
            Func<long, IIdentity> factory = GetFactoryForTag(tag);

            return (TIdentity)factory(await _counterService.GetNextAsync(tag));
        }

        private Func<long, IIdentity> GetFactoryForTag(string tag)
        {
            Func<long, IIdentity> factory;
            if (!_longBasedFactories.TryGetValue(tag, out factory))
            {
                throw new JarvisFrameworkEngineException(string.Format("{0} not registered in IdentityManager", tag));
            }

            return factory;
        }

        public bool TryParse<T>(string id, out T typedIdentity) where T : IIdentity
        {
            typedIdentity = default;
            if (!TryGetTag(id, out var tag, out var longId, out var factoryFunc))
            {
                return false;
            }

            //ok we can generate the id but we need to check if returned id is of the correct type.
            var createdIdentity = factoryFunc(longId);
            if (createdIdentity is T correctIdentity)
            {
                typedIdentity = correctIdentity;
                return true;
            }

            return false;
        }

        public Task ForceNextIdAsync<TIdentity>(long nextIdToReturn)
        {
            var tag = EventStoreIdentity.GetTagForIdentityClass(typeof(TIdentity));
            return _counterService.ForceNextIdAsync(tag, nextIdToReturn);
        }

        public Task<long> PeekNextIdAsync<TIdentity>()
        {
            var tag = EventStoreIdentity.GetTagForIdentityClass(typeof(TIdentity));
            return _counterService.PeekNextAsync(tag);
        }

        public bool TryGetTag(string id, out string tag)
        {
            return TryGetTag(id, out tag, out var _, out var __);
        }

        public Type GetIdentityTypeByTag(string tag)
        {
            if (string.IsNullOrEmpty(tag))
            {
                return null;
            }

            _tagToTypeMapping.TryGetValue(tag, out Type type);
            return type;
        }

        private bool TryGetTag(string id, out string tag, out long longId, out Func<long, IIdentity> factoryFunc)
        {
            longId = 0;
            tag = String.Empty;
            factoryFunc = null;
            if (string.IsNullOrEmpty(id))
            {
                return false;
            }

            //then we check for the separator, if not present is not an id.
            var pos = id.IndexOf(EventStoreIdentity.Separator);
            if (pos < 1)
            {
                return false;
            }

            //everything after the separator must be a digit
            StringBuilder sb = new StringBuilder(20);
            for (int i = pos + 1; i < id.Length; i++)
            {
                if (!char.IsDigit(id[i]))
                {
                    return false;
                }

                sb.Append(id[i]);
            }

            //now we need to parse the long id
            if (!long.TryParse(sb.ToString(), out longId))
            {
                return false;
            }

            tag = id.Substring(0, pos);
            if (!_longBasedFactories.TryGetValue(tag, out factoryFunc))
            {
                tag = string.Empty;
                return false;
            }
            return true;
        }
    }
}