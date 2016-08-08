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
        private readonly MongoClient client;

        public DatabaseHealthCheck(String dbDescription, String serverUrl)
           : base("MongoDatabaseCheck: " + dbDescription)
        {
            var url = new MongoUrl(serverUrl);
            client = new MongoClient(url);
            HealthChecks.RegisterHealthCheck(this);
        }

        protected override HealthCheckResult Check()
        {
            var state = this.client.Cluster.Description.State;
            if (state == MongoDB.Driver.Core.Clusters.ClusterState.Connected)
                return HealthCheckResult.Healthy();

            return HealthCheckResult.Unhealthy("Unable to connect to Mongo Db Instance.!");
        }
    }
}
