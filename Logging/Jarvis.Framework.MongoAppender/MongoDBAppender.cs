using System;
using log4net.Appender;
using log4net.Core;
using MongoDB.Driver;

namespace Jarvis.Framework.MongoAppender
{
    public interface IMongoAppenderCollectionProvider
    {
        MongoCollection GetCollection();
    }

    public class MongoDBAppender : AppenderSkeleton, IMongoAppenderCollectionProvider
    {
        public MongoLog Settings { get; set; }

        protected override bool RequiresLayout
        {
            get { return false; }
        }

        private Boolean _initializationFailed;

        public override void ActivateOptions()
        {
            try
            {
                Settings.SetupCollection();
            }
            catch (Exception e)
            {
                _initializationFailed = true;
                ErrorHandler.Error("Exception while initializing MongoDB Appender", e, ErrorCode.GenericFailure);
            }
        }

        protected override void Append(LoggingEvent loggingEvent)
        {
            //there is no meaning to try writing on mongo if initialization failed.
            if (!_initializationFailed)
            {
                Settings.Insert(loggingEvent);
            }
        }

        public MongoCollection GetCollection()
        {
            return Settings.LogCollection;
        }
    }
}