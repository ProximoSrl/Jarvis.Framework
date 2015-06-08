using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jarvis.MonitoringAgent.Support
{
    public class MonitoringAgentConfiguration
    {
        public Role Role { get; protected set; }

        /// <summary>
        /// this is the primary connection string, used to save data 
        /// related to server or agents functionalities.
        /// </summary>
        public String MongoConnectionString { get; protected set; }

        public String ServerWebAppAddress { get; protected set; }

        /// <summary>
        /// this is the list of mongo database with logs that the agent
        /// should send to the server.
        /// </summary>
        public List<MongoLogDatabase> MongoLogDatabaseList { get; set; }

        public class MongoLogDatabase
        {
            public String ConnectionString { get; set; }

            public String CollectionName { get; set; }
        }
    }

    public enum Role
    {
        Server,
        Agent,
    }

    public class AppConfigMonitoringAgentConfiguration : MonitoringAgentConfiguration
    {
        public AppConfigMonitoringAgentConfiguration()
        {
            Role = (Role)Enum.Parse(typeof(Role), ConfigurationManager.AppSettings["role"]);
            MongoConnectionString = ConfigurationManager.ConnectionStrings["mongo"].ConnectionString;
            ServerWebAppAddress = ConfigurationManager.AppSettings["server.appuri"];

            MongoLogDatabaseList = new List<MongoLogDatabase>();
            foreach (var mongoLogSetting in ConfigurationManager.AppSettings
                .AllKeys.Where(k => k.StartsWith("mongo-log")))
            {
                var setting = ConfigurationManager.AppSettings[mongoLogSetting];
                var splittedSetting = setting.Split('|');
                MongoLogDatabaseList.Add(new MongoLogDatabase()
                {
                    ConnectionString = splittedSetting[0],
                    CollectionName = splittedSetting[1],
                });
            }
        }
    }
}
