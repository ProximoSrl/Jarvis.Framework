using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Fasterflect;
using Jarvis.NEventStoreEx.CommonDomainEx;
using System.Linq.Expressions;
using Jarvis.Framework.Shared.Helpers;
using Castle.Core.Logging;

namespace Jarvis.Framework.Shared.IdentitySupport
{
    public class IdentityManager : IIdentityManager
    {
        readonly ICounterService _counterService;

        private readonly Dictionary<string, Func<long, IIdentity>> _longBasedFactories = new Dictionary<string, Func<long, IIdentity>>();

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
                throw new Exception(string.Format("invalid identity value {0}", identityAsString));
            var tag = identityAsString.Substring(0, pos);
            var id = Int64.Parse(identityAsString.Substring(pos + 1));
            Func<Int64, IIdentity> factory;
            if (!_longBasedFactories.TryGetValue(tag, out factory))
            {
                throw new Exception(string.Format("{0} not registered in IdentityManager", tag));
            }
            return factory(id);
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