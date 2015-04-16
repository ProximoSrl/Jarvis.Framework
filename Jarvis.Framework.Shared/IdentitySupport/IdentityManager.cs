using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Fasterflect;
using Jarvis.NEventStoreEx.CommonDomainEx;

namespace Jarvis.Framework.Shared.IdentitySupport
{
    public class IdentityManager : IIdentityManager
    {
        readonly ICounterService _counterService;
        private readonly Dictionary<string, Func<string, IIdentity>> _stringBasedFactories = new Dictionary<string, Func<string, IIdentity>>();
        private readonly Dictionary<string, Func<long, IIdentity>> _longBasedFactories = new Dictionary<string, Func<long, IIdentity>>();

        public IdentityManager(ICounterService counterService)
        {
            _counterService = counterService;
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
                throw new Exception(string.Format("invalid identity value {0}", identityAsString));
            var tag = identityAsString.Substring(0, pos);
            Func<string, IIdentity> factory;
            if (!_stringBasedFactories.TryGetValue(tag, out factory))
            {
                throw new Exception(string.Format("{0} not registered in IdentityManager", tag));
            }
            return factory(identityAsString);
        }

        public TIdentity ToIdentity<TIdentity>(string identityAsString)
        {
            return (TIdentity)this.ToIdentity(identityAsString);
        }

        public void RegisterIdentitiesFromAssembly(Assembly assembly)
        {
            var sb = new StringBuilder();
            foreach (var ic in assembly.GetTypes().Where(x => typeof(IIdentity).IsAssignableFrom(x) && !x.IsAbstract && x.IsClass))
            {
                var tag = EventStoreIdentity.GetTagForIdentityClass(ic);

                // registra la factory per string
                {
                    var ctor = ic.Constructor(new Type[] { typeof(string) });
                    if (ctor == null)
                    {
                        sb.AppendFormat("Identity {0} must have the constructor {1}(string id)\n", ic.FullName, ic.Name);
                    }
                    _stringBasedFactories[tag] = (id) => (EventStoreIdentity)ctor.CreateInstance(new object[] { id });
                }

                // registra la factory per long
                {
                    var ctor = ic.Constructor(new Type[] { typeof(long) });
                    if (ctor == null)
                    {
                        sb.AppendFormat("Identity {0} must have the constructor {1}(long id)\n", ic.FullName, ic.Name);
                    }
                    _longBasedFactories[tag] = (id) => (EventStoreIdentity)ctor.CreateInstance(new object[] { id });
                }
            }

            var errors = sb.ToString();
            if (!String.IsNullOrWhiteSpace(errors))
                throw new Exception("Found identities with errors:\n" + errors);
        }

        public TIdentity New<TIdentity>()
        {
            var tag = EventStoreIdentity.GetTagForIdentityClass(typeof(TIdentity));

            Func<long, IIdentity> factory;
            if (!_longBasedFactories.TryGetValue(tag, out factory))
            {
                throw new Exception(string.Format("{0} not registered in IdentityManager", tag));
            }
            return (TIdentity)factory(_counterService.GetNext(tag));
        }

	    public bool TryParse<T>(string id, out T typedIdentity) where T: class, IIdentity
	    {
		    try
		    {
			    typedIdentity = ToIdentity<T>(id);
                if(typedIdentity == null)
		        {
		            return false;
                }
			    return true;
		    }
// ReSharper disable EmptyGeneralCatchClause
		    catch 
// ReSharper restore EmptyGeneralCatchClause
		    {
		    }

		    typedIdentity = default(T);
			return false;
		}
    }
}