using Metrics;
using Metrics.Core;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jarvis.Framework.Kernel.Support
{
    public class DatabaseHealthCheck : HealthCheck
    {
        private readonly MongoServer mongoServer;

        public DatabaseHealthCheck(String dbDescription, MongoServer server)
            : base("MongoDatabaseCheck:" + dbDescription)
        {
            this.mongoServer = server;
            HealthChecks.RegisterHealthCheck(this);
        }

        public DatabaseHealthCheck(String dbDescription, String serverUrl)
           : base("MongoDatabaseCheck:" + dbDescription)
        {
            var url = new MongoUrl(serverUrl);
            var client = new MongoClient(url);
            this.mongoServer = client.GetServer();
            HealthChecks.RegisterHealthCheck(this);
        }

        protected override HealthCheckResult Check()
        {
            this.mongoServer.Ping();
            return HealthCheckResult.Healthy();
        }
    }
}
