using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jarvis.Framework.Kernel.Support
{
    public class MongoConnectionsManager
    {
        public struct ConnectionInfo
        {
            public String Name { get; set; }

            public String ConnectionString { get; set; }

            public ConnectionInfo(String name, String connectionString)
            {
                Name = name;
                ConnectionString = connectionString;
            }
        }

        private ConnectionInfo[] _connectionStrings;

        public MongoConnectionsManager(ConnectionInfo[] connectionStrings)
        {
            _connectionStrings = connectionStrings;
            SetupHealthCheck();
        }

        private void SetupHealthCheck()
        {
            foreach (var connection in _connectionStrings)
            {
                new DatabaseHealthCheck(connection.Name, connection.ConnectionString);
            }
        }

        public bool CheckDatabase()
        {
            foreach (var connection in _connectionStrings)
            {
                if (!CheckConnection(connection.ConnectionString))
                    return false;
            }
            return true;
        }

        private Boolean CheckConnection(String connection)
        {
            var url = new MongoUrl(connection);
            var client = new MongoClient(url);
            var state = client.Cluster.Description.State;
            return state == MongoDB.Driver.Core.Clusters.ClusterState.Connected;
        }


    }
}
