using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using Castle.Facilities.Logging;
using Castle.Services.Logging.Log4netIntegration;

namespace Jarvis.Framework.LogViewer.Host.Support
{
    public class LogViewerConfiguration
    {
        public String MongoDbConnection { get; set; }

        public String MongoDbDefaultCollectionLog { get; set; }

        public LogViewerConfiguration()
        {
            MongoDbDefaultCollectionLog = "logs";
            ServerAddressList = ConfigurationManager.AppSettings["endPoints"];
            MongoDbConnection = ConfigurationManager.ConnectionStrings["mongo"].ConnectionString;
        }

        public String ServerAddressList { get; set; }

        public void CreateLoggingFacility(LoggingFacility f)
        {
            f.LogUsing(new ExtendedLog4netFactory("log4net.config"));
        }
    }
}
