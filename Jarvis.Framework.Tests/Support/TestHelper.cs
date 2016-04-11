using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MongoDB.Driver;
using Jarvis.Framework.Shared.Helpers;

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
    }
}
