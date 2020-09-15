using Jarvis.Framework.Shared.Support;
using Metrics;
using Metrics.Core;
using MongoDB.Driver;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Jarvis.Framework.Kernel.Support
{
    public class DatabaseHealthCheck : HealthCheck
    {
        private readonly String _dbDescription;
        private readonly String _serverUrl;

        public DatabaseHealthCheck(
            String dbDescription,
            String serverUrl)
           : base("MongoDatabaseCheck: " + dbDescription)
        {
            _dbDescription = dbDescription;
            HealthChecks.RegisterHealthCheck(this);
            _serverUrl = serverUrl;
        }

        private string _lastError = null;
        DateTime _lastQuestionTime = DateTime.MinValue;
        protected override HealthCheckResult Check()
        {
            if (DateTime.Now.Subtract(_lastQuestionTime).TotalSeconds > 20)
            {
                _lastQuestionTime = DateTime.Now;
                Task.Factory.StartNew(() => PerformCheck());
            }

            if (!String.IsNullOrEmpty(_lastError))
                return HealthCheckResult.Unhealthy(_lastError.Replace("{", "{{").Replace("}", "}}"));

            return HealthCheckResult.Healthy();
        }

        private void PerformCheck()
        {
            try
            {
                var url = new MongoUrl(_serverUrl);
                var client = url.CreateClient(false);
                client.ListDatabases();

                var state = client.Cluster.Description.State;
                if (state == MongoDB.Driver.Core.Clusters.ClusterState.Connected)
                {
                    //Connection ok, but replicaset???
                    var disconnectedNodes = client.Cluster
                       .Description
                       .Servers
                       .Where(_ => _.State == MongoDB.Driver.Core.Servers.ServerState.Disconnected)
                       .ToList();
                    if (disconnectedNodes.Count > 0)
                    {
                        _lastError = String.Format("Replica set is on but these members are down: {0}!",
                             String.Join(", ", disconnectedNodes.Select(_ => _.ServerId?.ToString())));
                    }
                    else
                    {
                        _lastError = null;
                    }
                }
                else
                {
                    _lastError = String.Format("Unable to connect to Mongo Db Instance {0}!", _dbDescription);
                }
            }
            catch (Exception ex)
            {
                _lastError = String.Format("Exception in connection {0}", ex.Message);
            }
        }
    }
#pragma warning restore S101 // Types should be named in camel case
}