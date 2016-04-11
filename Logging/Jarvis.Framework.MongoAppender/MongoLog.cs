using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Linq;
using log4net.Core;
using log4net.Util;
using MongoDB.Bson;
using MongoDB.Driver;

using System.Reflection;

namespace Jarvis.Framework.MongoAppender
{
    public class MongoLog
    {
        public class TimeToLive
        {
            public int Days { get; set; }
            public int Hours { get; set; }
            public int Minutes { get; set; }

            public TimeSpan ToTimeSpan()
            {
                return new TimeSpan(Days, Hours, Minutes, 0);
            }
        }

        public MongoLog()
        {
            var entryAssembly = Assembly.GetEntryAssembly();
            if (entryAssembly != null)
            {
                _programName = entryAssembly.GetName().Name;
            }
            else
            {
                _programName = Path.GetFileNameWithoutExtension(Process.GetCurrentProcess().MainModule.FileName);
            }
        }

        string _machineName;

        public string MachineName
        {
            get
            {
                if (_machineName == null)
                    _machineName = Environment.MachineName;

                return _machineName;
            }
            private set { _machineName = value; }
        }

        public string CappedSizeInMb
        {
            get
            {
                if (_cappedSizeInMb == null)
                    _cappedSizeInMb = (5 * 1024).ToString();

                return _cappedSizeInMb;
            }
            private set { _cappedSizeInMb = value; }
        }
        private string _cappedSizeInMb;

        public string ConnectionString { get; set; }
        public string CollectionName { get; set; }

        public TimeToLive ExpireAfter { get; set; }

        public Boolean LooseFix { get; set; }

        public void SetupCollection()
        {
            var uri = new MongoUrl(ConnectionString);
            var client = new MongoClient(uri);
            IMongoDatabase db = client.GetDatabase(uri.DatabaseName);
            Int64 cappedSize;
            if (!Int64.TryParse(CappedSizeInMb, out cappedSize))
            {
                cappedSize = 5 * 1024L;
            }
            var collections = db.ListCollections()
                .ToList()
                .Select(d => d["name"].AsString)
                .ToList();
            if (!collections.Contains(CollectionName))
            {
                var craeteCollectionOptions = new CreateCollectionOptions() {
                    Capped = true,
                    MaxSize = 1024L * 1024L * cappedSize
                };
                db.CreateCollection(CollectionName, craeteCollectionOptions);
            }
            LogCollection = db.GetCollection<BsonDocument>(CollectionName);
            CreateIndexOptions options = new CreateIndexOptions();
            const string ttlIndex = FieldNames.Timestamp + "_-1";
            var index = LogCollection.Indexes.List().ToList().SingleOrDefault(x => x["name"].AsString == ttlIndex);
            if (index != null)
            {
                //if (ExpireAfter != null)
                //{
                //    if (index.op != ExpireAfter.ToTimeSpan())
                //    {
                //        var d = new CommandDocument()
                //        {
                //            {   "collMod", CollectionName   },
                //            {
                //                "index", new BsonDocument
                //                {
                //                    {"keyPattern", new BsonDocument {{FieldNames.Timestamp, -1}}},
                //                    {"expireAfterSeconds", (int)(ExpireAfter.ToTimeSpan().TotalSeconds)}
                //                }
                //            }
                //        };

                //        db.RunCommand(d);
                //    }
                //}
            }
            else
            {
                if (ExpireAfter != null)
                {
                    options.ExpireAfter = ExpireAfter.ToTimeSpan();
                }

                LogCollection.Indexes.CreateOne(Builders<BsonDocument>.IndexKeys.Descending(FieldNames.Timestamp), options);
            }

            LogCollection.Indexes.CreateOne(Builders<BsonDocument>.IndexKeys
                .Ascending(FieldNames.Level).Ascending(FieldNames.Thread).Ascending(FieldNames.Loggername));
        }

        public BsonDocument LoggingEventToBSON(LoggingEvent loggingEvent)
        {
            if (loggingEvent == null) return null;

            var toReturn = new BsonDocument();
            toReturn[FieldNames.Timestamp] = loggingEvent.TimeStamp;
            toReturn[FieldNames.Level] = loggingEvent.Level.ToString();
            toReturn[FieldNames.Thread] = loggingEvent.ThreadName ?? String.Empty;
            if (!LooseFix)
            {
                toReturn[FieldNames.Username] = loggingEvent.UserName ?? String.Empty;
            }

            toReturn[FieldNames.Message] = loggingEvent.RenderedMessage;
            toReturn[FieldNames.Loggername] = loggingEvent.LoggerName ?? String.Empty;
            toReturn[FieldNames.Domain] = loggingEvent.Domain ?? String.Empty;
            toReturn[FieldNames.Machinename] = MachineName ?? String.Empty;
            toReturn[FieldNames.ProgramName] = ProgramName ?? String.Empty;

            // location information, if available
            if (!LooseFix && loggingEvent.LocationInformation != null)
            {
                toReturn[FieldNames.Filename] = loggingEvent.LocationInformation.FileName ?? String.Empty;
                toReturn[FieldNames.Method] = loggingEvent.LocationInformation.MethodName ?? String.Empty;
                toReturn[FieldNames.Linenumber] = loggingEvent.LocationInformation.LineNumber ?? String.Empty;
                toReturn[FieldNames.Classname] = loggingEvent.LocationInformation.ClassName ?? String.Empty;
            }

            // exception information
            if (loggingEvent.ExceptionObject != null)
            {
                toReturn[FieldNames.Exception] = ExceptionToBSON(loggingEvent.ExceptionObject);
                if (loggingEvent.ExceptionObject.InnerException != null)
                {
                    //Serialize all inner exception in a bson array
                    var innerExceptionList = new BsonArray();
                    var actualEx = loggingEvent.ExceptionObject.InnerException;
                    while (actualEx != null)
                    {
                        var ex = ExceptionToBSON(actualEx);
                        innerExceptionList.Add(ex);
                        if (actualEx.InnerException == null)
                        {
                            //this is the first exception
                            toReturn[FieldNames.FirstException] = ExceptionToBSON(actualEx);
                        }
                        actualEx = actualEx.InnerException;
                    }
                    toReturn[FieldNames.Innerexception] = innerExceptionList;
                }
            }

            // properties
            PropertiesDictionary compositeProperties = loggingEvent.GetProperties();
            if (compositeProperties != null && compositeProperties.Count > 0)
            {
                var properties = new BsonDocument();
                foreach (DictionaryEntry entry in compositeProperties)
                {
                    if (entry.Value == null) continue;

                    //remember that no property can have a point in it because it cannot be saved.
                    String key = entry.Key.ToString().Replace(".", "|");
                    BsonValue value;
                    if (!BsonTypeMapper.TryMapToBsonValue(entry.Value, out value))
                    {
                        properties[key] = entry.Value.ToBsonDocument();
                    }
                    else
                    {
                        properties[key] = value;
                    }
                }
                toReturn[FieldNames.Customproperties] = properties;
            }

            return toReturn;
        }

        /// <summary>
        /// Create BSON representation of Exception
        ///     
        /// </summary>
        /// <param name="ex"></param>
        /// <returns></returns>
        protected BsonDocument ExceptionToBSON(Exception ex)
        {
            var toReturn = new BsonDocument();
            toReturn[FieldNames.Message] = ex.Message;
            toReturn[FieldNames.Source] = ex.Source ?? string.Empty;
            toReturn[FieldNames.Stacktrace] = ex.StackTrace ?? string.Empty;

            return toReturn;
        }

        public IMongoCollection<BsonDocument> LogCollection { get; private set; }

        /// <summary>
        /// Used to programmatically override the name of the program with code.
        /// </summary>
        /// <param name="programName"></param>
        public static void SetProgramName(String programName)
        {
            _programName = programName;
        }

        static string _programName;

        public string ProgramName
        {
            get
            {
                return _programName;
            }
            set { _programName = value; }
        }


        public void InsertBatch(LoggingEvent[] events)
        {
            if (LogCollection != null)
            {
                var docs = events.Select(LoggingEventToBSON).Where(x => x != null).ToArray();
                LogCollection.InsertMany(docs);
            }
        }

        public void Insert(LoggingEvent loggingEvent)
        {
            if (LogCollection != null)
            {
                BsonDocument doc = LoggingEventToBSON(loggingEvent);
                if (doc != null)
                {
                    LogCollection.InsertOne(doc);
                }
            }
        }

        public void Insert(BsonDocument doc)
        {

                    LogCollection.InsertOne(doc);

        }
    }
}