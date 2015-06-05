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

        public String MongoConnectionString { get; protected set; }

        public String ServerWebAppAddress { get; protected set; }
    }

    public enum Role
    {
        Server,
        Monitor,
    }

    public class AppConfigMonitoringAgentConfiguration : MonitoringAgentConfiguration
    {
        public AppConfigMonitoringAgentConfiguration()
        {
            Role = (Role)Enum.Parse(typeof(Role), ConfigurationManager.AppSettings["role"]);
            MongoConnectionString = ConfigurationManager.ConnectionStrings["mongo"].ConnectionString;
            ServerWebAppAddress = ConfigurationManager.AppSettings["server.appuri"];
        }
    }
}
