using System;
using System.Collections.Generic;
using Jarvis.Framework.Shared.IdentitySupport;
using Jarvis.Framework.Shared.MultitenantSupport;
using MongoDB.Driver;

namespace Jarvis.Framework.Kernel.MultitenantSupport
{
    public class TenantSettings 
    {
        public TenantId TenantId { get; private set; }
        public ICounterService CounterService { get; private set; }

        readonly IDictionary<string, object> _settings;
        public TenantSettings(TenantId tenantId)
        {
            TenantId = tenantId;
            _settings = new Dictionary<string, object>();
        }

        public virtual void Init()
        {
            CounterService = CreateCounterService();
        }

        protected virtual ICounterService CreateCounterService()
        {
            return new CounterService(GetDatabase("system"));
        }

        public virtual string GetConnectionString(string name)
        {
            var connectionString = Get<string>("connectionstring."+name);
            if(connectionString == null)
                throw new Exception("Connection string "+ name + " not found");
            return connectionString;
        }

        protected MongoDatabase GetDatabase(string connectionStringName)
        {
            var url = new MongoUrl(GetConnectionString(connectionStringName));
            var client = new MongoClient(url);
            return client.GetServer().GetDatabase(url.DatabaseName);
        }

        public void Set(string key, object value)
        {
            _settings[key] = value;
        }

        public T Get<T>(string key)
        {
            return (T) _settings[key];
        }
    }
}