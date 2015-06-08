using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;

namespace Jarvis.MonitoringAgentServer.Support
{
    public class MonitoringAgentServerConfiguration
    {
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

        public DirectoryInfo UploadQueueFolder { get; protected set; }

        public class MongoLogDatabase
        {
            public String ConnectionString { get; set; }

            public String CollectionName { get; set; }
        }
    }



    public class AppConfigMonitoringAgentServerConfiguration : MonitoringAgentServerConfiguration
    {
        public AppConfigMonitoringAgentServerConfiguration()
        {
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

            UploadQueueFolder = new DirectoryInfo(ConfigurationManager.AppSettings["upload-temp-folder"]);
            if (!UploadQueueFolder.Exists)
            {
                Directory.CreateDirectory(UploadQueueFolder.FullName);
            }
        }
    }
}
