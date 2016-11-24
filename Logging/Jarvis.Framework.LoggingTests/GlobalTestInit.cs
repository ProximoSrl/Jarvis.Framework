using System;
using System.Configuration;
using NUnit.Framework;

namespace Jarvis.Framework.LoggingTests
{
    [SetUpFixture]
    public class GlobalSetup
    {
        [SetUp]
        public void ShowSomeTrace()
        {
            var overrideTestDb = Environment.GetEnvironmentVariable("TEST_MONGODB");
            if (String.IsNullOrEmpty(overrideTestDb)) return;

            var overrideTestDbQueryString = Environment.GetEnvironmentVariable("TEST_MONGODB_QUERYSTRING");
            var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            var connectionStringsSection = (ConnectionStringsSection)config.GetSection("connectionStrings");

            connectionStringsSection.ConnectionStrings["testDb"].ConnectionString = overrideTestDb.TrimEnd('/') + "/{0}" + overrideTestDbQueryString;
            config.Save();
            ConfigurationManager.RefreshSection("connectionStrings");
        }
    }
}
