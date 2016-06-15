using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MongoDB.Driver;
using Jarvis.Framework.Shared.Helpers;
using System.Configuration;
using Jarvis.Framework.Shared.IdentitySupport;
using System.Reflection;
using Jarvis.Framework.Shared.IdentitySupport.Serialization;
using MongoDB.Bson.Serialization;

namespace Jarvis.Framework.Tests.Support
{
    public static class TestHelper
    {
        public static IMongoDatabase CreateNew(string connectionString)
        {
            var url = new MongoUrl(connectionString);
            var client = new MongoClient(url);
            var db = client.GetDatabase(url.DatabaseName);
            
            db.Drop();

            return db;
        }

        private static IdentityManager _manager;
        private static HashSet<Type> _registeredTypes = new HashSet<Type>();

        public static void RegisterSerializerForFlatId<T>() where T : EventStoreIdentity
        {
            if (_manager == null)
            {
                var systemDb = TestHelper.CreateNew(ConfigurationManager.ConnectionStrings["system"].ConnectionString);
                _manager = new IdentityManager(new CounterService(systemDb));
                _manager.RegisterIdentitiesFromAssembly(Assembly.GetExecutingAssembly());
                MongoFlatIdSerializerHelper.Initialize(_manager);
            }
            if (!_registeredTypes.Contains(typeof(T)))
            {
                _registeredTypes.Add(typeof(T));
                BsonSerializer.RegisterSerializer(typeof(T), new TypedEventStoreIdentityBsonSerializer<T>());
                EventStoreIdentityCustomBsonTypeMapper.Register<T>();
            }
           
        }
    }
}
