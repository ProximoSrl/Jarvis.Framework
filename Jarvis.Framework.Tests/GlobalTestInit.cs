using System;
using System.Configuration;
using NUnit.Framework;
using Jarvis.Framework.Kernel.Support;
using Jarvis.Framework.Shared.IdentitySupport;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace Jarvis.Framework.Tests
{
    [SetUpFixture]
    public class GlobalSetup
    {
        [SetUp]
        public void Global_initialization_of_all_tests()
        {
            var objectSerializer = new ObjectSerializer(type => ObjectSerializer.DefaultAllowedTypes(type) || type.FullName.StartsWith("Jarvis"));
            BsonSerializer.RegisterSerializer(objectSerializer);
            MongoRegistration.RegisterMongoConversions(
                "NEventStore.Persistence.MongoDB"
                );
            MongoFlatMapper.EnableFlatMapping(true);
            MongoRegistration.RegisterAssembly(GetType().Assembly);
            var overrideTestDb = Environment.GetEnvironmentVariable("TEST_MONGODB");
            if (String.IsNullOrEmpty(overrideTestDb)) return;

            Console.WriteLine("Mongodb database is overriden with TEST_MONGODB environment variable:" + overrideTestDb);
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
}

