using Castle.Core.Logging;
using MongoDB.Driver;
using MongoDB.Driver.Core.Clusters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Jarvis.Framework.Kernel.Support
{
	public class MongoConnectionsManager
	{
		public ILogger Logger { get; set; }

		public struct ConnectionInfo
		{
			public String Name { get; set; }

			public String ConnectionString { get; set; }

			public ConnectionInfo(String name, String connectionString) : this()
			{
				Name = name;
				ConnectionString = connectionString;
			}
		}

		private readonly ConnectionInfo[] _connectionStrings;

		public MongoConnectionsManager(ConnectionInfo[] connectionStrings)
		{
			Logger = NullLogger.Instance;
			_connectionStrings = connectionStrings;
			SetupHealthCheck();
		}

		private List<DatabaseHealthCheck> _healthChecks;

		private void SetupHealthCheck()
		{
			_healthChecks = new List<DatabaseHealthCheck>();

			foreach (var connection in _connectionStrings)
			{
				_healthChecks.Add(new DatabaseHealthCheck(connection.Name, connection.ConnectionString));
			}
		}

		public bool CheckDatabase()
		{
			foreach (var connection in _connectionStrings)
			{
				if (!CheckConnection(connection.ConnectionString))
				{
					Logger.DebugFormat("Check database failed for connection {0}", connection.ConnectionString);
					return false;
				}
			}
			return true;
		}

		private Boolean CheckConnection(String connection)
		{
			var url = new MongoUrl(connection);
			var client = new MongoClient(url);
			Task.Factory.StartNew(() => client.ListDatabases()); //forces a database connection
			Int32 spinCount = 0;
			ClusterState clusterState;

			while ((clusterState = client.Cluster.Description.State) != ClusterState.Connected &&
				spinCount++ < 100)
			{
				Thread.Sleep(20);
			}
			return clusterState == ClusterState.Connected;
		}
	}
}
