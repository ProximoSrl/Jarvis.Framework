using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MongoDB.Driver;

namespace Jarvis.Framework.Tests.Support
{
    public static class TestHelper
    {
        public static MongoDatabase CreateNew(string connectionString)
        {
            var url = new MongoUrl(connectionString);
            var server = new MongoClient(url).GetServer();
            var db = server.GetDatabase(url.DatabaseName);
            
            db.Drop();

            return db;
        }
    }
}
