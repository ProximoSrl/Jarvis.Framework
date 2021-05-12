using System;
using System.Configuration;
using NUnit.Framework;
using Jarvis.Framework.Kernel.Support;
using Jarvis.Framework.Shared.IdentitySupport;
using System.IO;
using System.Reflection;
using Jarvis.Framework.TestHelpers;
using Jarvis.Framework.Shared.Support;
using App.Metrics;

namespace Jarvis.Framework.Tests
{
    [SetUpFixture]
    public class GlobalSetup
    {
        [OneTimeSetUp]
        public void Global_initialization_of_all_tests()
        {
            MetricsHelper.InitMetrics(new MetricsBuilder());

            //Nunit3 fix for test adapter of visual studio, it uses visual studio test directory
            Environment.CurrentDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            MongoRegistration.ConfigureMongoForJarvisFramework(
                "NEventStore.Persistence.MongoDB"
                );
            MongoFlatMapper.EnableFlatMapping(true);
            MongoRegistration.RegisterAssembly(GetType().Assembly);
            var overrideTestDb = Environment.GetEnvironmentVariable("TEST_MONGODB");
            if (String.IsNullOrEmpty(overrideTestDb)) return;

            Console.WriteLine("Mongodb database is overriden with TEST_MONGODB environment variable:" + overrideTestDb);
            var overrideTestDbQueryString = Environment.GetEnvironmentVariable("TEST_MONGODB_QUERYSTRING") ?? "";
            overrideTestDbQueryString = overrideTestDbQueryString.Trim();
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

            TestLogger.GlobalEnabled = false;
        }
    }
}

