using Castle.Core.Logging;
using Jarvis.Framework.Shared.Helpers;
using System;
using System.Collections.Generic;

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
        }

        public bool CheckDatabase()
        {
            foreach (var connection in _connectionStrings)
            {
                if (!MongoDriverHelper.CheckConnection(connection.ConnectionString))
                {
                    Logger.DebugFormat("Check database failed for connection {0}", connection.ConnectionString);
                    return false;
                }
            }
            return true;
        }
    }
}
