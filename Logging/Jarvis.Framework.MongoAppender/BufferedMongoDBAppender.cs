using System;
using log4net.Appender;
using log4net.Core;
using MongoDB.Driver;
using System.Collections.Concurrent;
using System.Threading;
using MongoDB.Bson;
using System.Threading.Tasks;

namespace Jarvis.Framework.MongoAppender
{
    public class BufferedMongoDBAppender : BufferingAppenderSkeleton, IMongoAppenderCollectionProvider
    {
        public BufferedMongoDBAppender()
        {
            MaxNumberOfObjectInBuffer = 100000; //100k of objects in buffer.
        }
        private BlockingCollection<LoggingEvent[]> _dispatchCollection;

        private void Dispatch(object obj)
        {
            foreach (var events in _dispatchCollection.GetConsumingEnumerable())
            {
                if (_initializationFailed) 
                {
                    //Since we are in a different thread we can pay the price of trying to reinitialize everything.
                    Initialize();
                }
                TryInsertBatch(events);
            }
        }

        public MongoLog Settings { get; set; }

        public Boolean SaveOnDifferentThread { get; set; }

        public Int32 MaxNumberOfObjectInBuffer { get; set; }

        private Int32 _maxBufferSize;

        protected override bool RequiresLayout
        {
            get { return false; }
        }

        private Boolean _initializationFailed;

        public override void ActivateOptions()
        {
            Initialize();
            base.ActivateOptions();
        }

        private Boolean _initializing = false;
        private void Initialize()
        {
            try
            {
                var collectionInitialized = Settings.SetupCollection();
                if (!collectionInitialized)
                {
                    _initializationFailed = true;
                    return;
                }

                if (Settings.LooseFix)
                {
                    //default Fix value is ALL, we need to avoid fixing some values that are
                    //too heavy. We skip FixFlags.UserName and FixFlags.LocationInfo
                    Fix = FixFlags.Domain | FixFlags.Exception | FixFlags.Identity |
                        FixFlags.Mdc | FixFlags.Message | FixFlags.Ndc |
                        FixFlags.Properties | FixFlags.ThreadName;
                }
                _maxBufferSize = MaxNumberOfObjectInBuffer / BufferSize;
                
                this.Evaluator = new log4net.Core.LevelEvaluator(Level.Error);
                _dispatchCollection = new BlockingCollection<LoggingEvent[]>();

                var pollerThread = new Thread(Dispatch);
                pollerThread.Name = "BufferedMongoDbAppender-Dispatcher";
                pollerThread.IsBackground = false;
                pollerThread.Start();
                _initializationFailed = false; //initialization successful.
            }
            catch (Exception e)
            {
                _initializationFailed = true;
                ErrorHandler.Error("Exception while initializing MongoDB Appender", e, ErrorCode.GenericFailure);
            }
        }

        protected override void SendBuffer(LoggingEvent[] events)
        {
            if (_initializationFailed) return;

            if (SaveOnDifferentThread)
            {
                if (_dispatchCollection != null && !_dispatchCollection.IsCompleted)
                    _dispatchCollection.Add(events);
                while (_dispatchCollection.Count > _maxBufferSize)
                    Thread.Sleep(100);
            }
            else 
            {
                TryInsertBatch(events);
            }
        }

        private void TryInsertBatch(LoggingEvent[] events)
        {
            if (_initializationFailed) return; 

            try
            {
                Settings.InsertBatch(events);
            }
            catch (Exception ex)
            {
                //I do not want to stop logging if some error occours.
            }
        }

        public override void Flush()
        {
            base.Flush();

            if (_initializationFailed) return;
            Int32 i = 0;
            while (_dispatchCollection.Count > 0 && i++ < 500) Thread.Sleep(50);
        }

        public override void Flush(bool flushLossyBuffer)
        {
            base.Flush(flushLossyBuffer);

            if (_initializationFailed) return;
            Int32 i = 0;
            while (_dispatchCollection.Count > 0 && i++ < 500) Thread.Sleep(50);
        }

        public IMongoCollection<BsonDocument> GetCollection()
        {
            return Settings.LogCollection;
        }

        protected override void OnClose()
        {
            base.OnClose();

            if (_initializationFailed) return;
            _dispatchCollection.CompleteAdding();
        }
    }
}