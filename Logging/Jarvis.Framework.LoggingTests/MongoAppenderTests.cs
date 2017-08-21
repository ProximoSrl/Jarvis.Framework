using System.Linq;
using Jarvis.Framework.MongoAppender;
using log4net;
using log4net.Appender;
using log4net.Core;
using log4net.Repository.Hierarchy;
using MongoDB.Bson;
using MongoDB.Driver;
using NUnit.Framework;
using System;
using log4net.Layout;
using System.Configuration;

namespace Jarvis.Framework.LoggingTests
{
    public class MongoAppenderTestsBaseClass
    {
        protected IMongoDatabase _db;
        protected IMongoCollection<BsonDocument> _logCollection;
        protected BufferedMongoDBAppender _appender;
        protected MongoDBAppender _mongoAppender;
        protected FileAppender _fileAppender;
        protected ILog _sut;
        protected Logger _logger;

        String connectionString;
        [TestFixtureSetUp]
        public void TestFixtureSetup()
        {
            connectionString = ConfigurationManager.ConnectionStrings["testDb"].ConnectionString;
            MongoUrl url = new MongoUrl(String.Format(connectionString, "test-db-log"));
            var client = new MongoClient(url);
            _db = client.GetDatabase(url.DatabaseName);
            _logCollection = _db.GetCollection<BsonDocument>("logs");
            _db.DropCollection(_logCollection.CollectionNamespace.CollectionName);

            var hierarchy = (Hierarchy)LogManager.CreateRepository("test");
            _logger = hierarchy.LoggerFactory.CreateLogger(hierarchy, "logname");
            _logger.Hierarchy = hierarchy;

            _logger.AddAppender(CreateMongoAppender(false, false));

            _logger.Repository.Configured = true;

            // alternative use the LevelMap to set the Log level based on a string
            // hierarchy.LevelMap["ERROR"]
            hierarchy.Threshold = Level.Debug;
            _logger.Level = Level.Debug;

            _sut = new LogImpl(_logger);


        }

        [SetUp]
        public void SetUp()
        {
            _db.DropCollection("logs");
        }

        protected IAppender CreateMongoAppender(Boolean looseFix, Boolean multiThreadSave)
        {
            _appender = new BufferedMongoDBAppender
            {
                Settings = new MongoLog()
                {
                    ConnectionString = String.Format(connectionString, "test-db-log"),
                    CollectionName = "logs",
                    LooseFix = looseFix,
                },
                SaveOnDifferentThread = multiThreadSave,
            };
            _appender.ActivateOptions();
            return _appender;
        }

        protected IAppender CreateMongoUnbufferedAppender(Boolean looseFix)
        {
            _mongoAppender = new MongoDBAppender
            {
                Settings = new MongoLog()
                {
                    ConnectionString = String.Format(connectionString, "test-db-log"),
                    CollectionName = "logs",
                    LooseFix = looseFix,
                }
            };
            _mongoAppender.ActivateOptions();
            return _mongoAppender;
        }



        protected IAppender CreateFileAppender()
        {
            _fileAppender = new FileAppender
            {
                AppendToFile = false,
                File = "test.log",
                Layout = new PatternLayout("%date %username [%thread] %-5level %logger [%property{NDC}] - %message%newline"),
            };
            _fileAppender.ActivateOptions();
            return _fileAppender;
        }

    }

    [TestFixture]
    public class MongoAppenderTests : MongoAppenderTestsBaseClass
    {

        [Test]  
        public void verify_single_log()
        {
            _sut.Debug("This is a logger");
            _appender.Flush();
            Assert.That(_logCollection.Count(Builders<BsonDocument>.Filter.Empty), Is.EqualTo(1));
        }
            
        [Test]
        public void verify_file_name()
        {
            _sut.Debug("This is a logger");
            _appender.Flush();
            var log = _logCollection.Find<BsonDocument>(Builders<BsonDocument>.Filter.Empty).Single();
            Assert.That(log["fi"].ToString(), Is.StringEnding("MongoAppenderTests.cs"));
        }

        [Test] 
        public void verify_lots_of_log()
        {
            for (int i = 0; i < 1000; i++)
            {
                _sut.Debug("This is a logger with a big string: " + new string('x', 10000));
            }
            _appender.Flush();
            var log = _logCollection.Find<BsonDocument>(Builders<BsonDocument>.Filter.Empty).Count();
            Assert.That(log, Is.EqualTo(1000));
        }

        [Test]
        public void verify_inner_exception()
        {
            try
            {
                var inner1 = new ApplicationException("Inner 1");
                var inner2 = new ApplicationException("Inner 2", inner1);
                throw new ApplicationException("outer", inner2);
            }
            catch (Exception ex)
            {
                _sut.Error("Exception", ex);
            }
            _appender.Flush();
            var log = _logCollection.Find<BsonDocument>(Builders<BsonDocument>.Filter.Empty).First();
            var innerExceptionList = log["ie"] as BsonArray;
            Assert.That(innerExceptionList.Count, Is.EqualTo(2));

            var exception = log["ex"].ToString();
            Assert.That(exception, Is.Not.StringContaining("Inner 1"), "Exception should not contain inner exception");
            Assert.That(exception, Is.Not.StringContaining("Inner 2"), "Exception should not contain inner exception");
        }

        [Test]
        public void verify_inner_exception_log_most_inner_exception()
        {
            try
            {
                var inner1 = new ApplicationException("Inner 1");
                var inner2 = new ApplicationException("Inner 2", inner1);
                var inner3 = new ApplicationException("Inner 3", inner2);
                throw new ApplicationException("outer", inner3);
            }
            catch (Exception ex)
            {
                _sut.Error("Exception", ex);
            }
            _appender.Flush();
            var log = _logCollection.Find<BsonDocument>(Builders<BsonDocument>.Filter.Empty).First();
            var firstException = log["fe"];
            Assert.That(firstException["me"].AsString, Is.EqualTo("Inner 1"));
        }
    }

    [TestFixture]
    [Explicit]
    public class MongoAppenderTestsLoadTest : MongoAppenderTestsBaseClass
    {
        //[Explicit]
        //[Test]
        //public void verify_speed_of_multiple_logs()
        //{
        //    Int32 iterationCount = 11000;
        //    Stopwatch w = new Stopwatch();
        //    w.Start();
        //    for (int i = 0; i < iterationCount; i++)
        //    {
        //        _sut.Debug("This is a logger");
        //    }
        //    Console.WriteLine("With MongoAppender - Before flush {0} ms", w.ElapsedMilliseconds);
        //    _appender.Flush();
        //    w.Stop();
        //    Console.WriteLine("With MongoAppender - Iteration took {0} ms", w.ElapsedMilliseconds);

        //    _logger.RemoveAllAppenders();
        //    _logger.AddAppender(CreateMongoAppender(true, false));
        //    w.Reset();
        //    w.Start();
        //    for (int i = 0; i < iterationCount; i++)
        //    {
        //        _sut.Debug("This is a logger");
        //    }
        //    Console.WriteLine("With MongoAppender Loose Fix - Before flush {0} ms", w.ElapsedMilliseconds);
        //    _appender.Flush();
        //    w.Stop();
        //    Console.WriteLine("With MongoAppender Loose Fix - Iteration took {0} ms", w.ElapsedMilliseconds);

        //    _logger.RemoveAllAppenders();
        //    _logger.AddAppender(CreateMongoAppender(false, true));
        //    w.Reset();
        //    w.Start();
        //    for (int i = 0; i < iterationCount; i++)
        //    {
        //        _sut.Debug("This is a logger");
        //    }
        //    Console.WriteLine("With MongoAppender save on different thread - Before flush {0} ms", w.ElapsedMilliseconds);
        //    _appender.Flush();
        //    w.Stop();
        //    Console.WriteLine("With MongoAppender save on different thread - Iteration took {0} ms", w.ElapsedMilliseconds);

        //    _logger.RemoveAllAppenders();
        //    _logger.AddAppender(CreateMongoAppender(true, true));
        //    w.Reset();
        //    w.Start();
        //    for (int i = 0; i < iterationCount; i++)
        //    {
        //        _sut.Debug("This is a logger");
        //    }
        //    Console.WriteLine("With MongoAppender Loose fix and save on different thread - Before flush {0} ms", w.ElapsedMilliseconds);
        //    _appender.Flush();
        //    w.Stop();
        //    Console.WriteLine("With MongoAppender Loose fix and  save on different thread - Iteration took {0} ms", w.ElapsedMilliseconds);


        //    _logger.RemoveAllAppenders();
        //    _logger.AddAppender(CreateMongoUnbufferedAppender(false));
        //    w.Reset();
        //    w.Start();
        //    for (int i = 0; i < iterationCount; i++)
        //    {
        //        _sut.Debug("This is a logger");
        //    }
        //    w.Stop();
        //    Console.WriteLine("With Mongo Unbuffered - Iteration took {0} ms", w.ElapsedMilliseconds);

        //    _logger.RemoveAllAppenders();
        //    _logger.AddAppender(CreateMongoUnbufferedAppender(true));
        //    w.Reset();
        //    w.Start();
        //    for (int i = 0; i < iterationCount; i++)
        //    {
        //        _sut.Debug("This is a logger");
        //    }
        //    w.Stop();
        //    Console.WriteLine("With Mongo Unbuffered loose fix - Iteration took {0} ms", w.ElapsedMilliseconds);


        //    _logger.RemoveAllAppenders();
        //    _logger.AddAppender(CreateFileAppender());
        //    w.Reset();
        //    w.Start();
        //    for (int i = 0; i < iterationCount; i++)
        //    {
        //        _sut.Debug("This is a logger");
        //    }
        //    Console.WriteLine("With FileAppender - Before flush {0} ms", w.ElapsedMilliseconds);
        //    _fileAppender.Close();
        //    w.Stop();
        //    Console.WriteLine("With FileAppender - Iteration took {0} ms", w.ElapsedMilliseconds);


        //    _logger.RemoveAllAppenders();
        //    w.Reset();
        //    w.Start();
        //    for (int i = 0; i < iterationCount; i++)
        //    {
        //        _sut.Debug("This is a logger");
        //    }
        //    w.Stop();
        //    Console.WriteLine("Without MongoAppender - Iteration took {0} ms", w.ElapsedMilliseconds);

        //}

        [Explicit]
        [Test]
        public void hammering_logger_to_verify_memory_consumption()
        {
            _logger.RemoveAllAppenders();
            BufferedMongoDBAppender appender =
                (BufferedMongoDBAppender)CreateMongoAppender(true, true);
            _logger.AddAppender(appender);
            String bigMessage = new String('X', 100000);
            var iterationCount = 1 * 1000;
            for (int i = 0; i < iterationCount; i++)
            {
                _sut.Debug(bigMessage);
            }
            appender.Flush();
        }
    }
}
