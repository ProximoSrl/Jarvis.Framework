using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


[SetUpFixture]
public class GlobalSetup
{
    [SetUp]
    public void Global_initialization_of_all_tests()
    {
        var overrideTestDb = Environment.GetEnvironmentVariable("TEST_MONGODB");
        if (String.IsNullOrEmpty(overrideTestDb)) return;

        var overrideTestDbQueryString = Environment.GetEnvironmentVariable("TEST_MONGODB_QUERYSTRING");
        var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
        var connectionStringsSection = (ConnectionStringsSection)config.GetSection("connectionStrings");
        connectionStringsSection.ConnectionStrings["eventstore"].ConnectionString = overrideTestDb.TrimEnd('/') + "/jarvis-framework-es-test" + overrideTestDbQueryString;
        connectionStringsSection.ConnectionStrings["saga"].ConnectionString = overrideTestDb.TrimEnd('/') + "/jarvis-framework-saga-test" + overrideTestDbQueryString;
        connectionStringsSection.ConnectionStrings["readmodel"].ConnectionString = overrideTestDb.TrimEnd('/') + "/jarvis-framework-readmodel-test" + overrideTestDbQueryString;
        connectionStringsSection.ConnectionStrings["system"].ConnectionString = overrideTestDb.TrimEnd('/') + "/jarvis-framework-system-test" + overrideTestDbQueryString;
        connectionStringsSection.ConnectionStrings["engine"].ConnectionString = overrideTestDb.TrimEnd('/') + "/jarvis-framework-engine-test" + overrideTestDbQueryString;
        connectionStringsSection.ConnectionStrings["rebus"].ConnectionString = overrideTestDb.TrimEnd('/') + "/jarvis-rebus-test" + overrideTestDbQueryString;
        connectionStringsSection.ConnectionStrings["log"].ConnectionString = overrideTestDb.TrimEnd('/') + "/jarvis-log-test" + overrideTestDbQueryString;

        config.Save();
        ConfigurationManager.RefreshSection("connectionStrings");
    }
}

