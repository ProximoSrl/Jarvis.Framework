using MongoDB.Bson;
using MongoDB.Driver;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MongoDB.Driver.Linq;
using MongoDB.Bson.Serialization.Attributes;
using System.Text.RegularExpressions;
using System.Globalization;

namespace Jarvis.Framework.ElasticLogPoller.Importers
{

    public class MongoImporter : BaseImporter
    {
        public const String DateTimeFormatForNestQuery = "yyyy-MM-ddTHH:mm:ss.fff";

        public class ImportCheckpoint
        {
            public DateTime LastCheckpoint { get; set; }

            [BsonId]
            public String CollectionName { get; set; }
        }
        public String Connection { get; set; }

        public String Collection { get; set; }

        public override string Type
        {
            get { return "mongo"; }
        }

        public override string Id
        {
            get { return String.Format("mongo: {0} collection {1}", Connection, Collection); }
        }

        private IMongoCollection<BsonDocument> _collection;
        private IMongoCollection<ImportCheckpoint> _checkpointCollection;
        private ImportCheckpoint _checkpoint;

        public override void Configure()
        {
            base.Configure();
            SetTtl("log", "30d");
            _log.InfoFormat("Mongo: Starting log polling {0}", Connection);
            IMongoDatabase db = GetDatabase();
            _collection = db.GetCollection<BsonDocument>(Collection);
            _checkpointCollection = db.GetCollection<ImportCheckpoint>("importer.checkpoints");
            _checkpoint = _checkpointCollection.AsQueryable()
                .SingleOrDefault(d => d.CollectionName == Collection);
            if (_checkpoint == null)
            {
                _checkpoint = new ImportCheckpoint() { CollectionName = Collection, LastCheckpoint = DateTime.MinValue };
            }
        }

        public override List<BaseImporter> HandleWildcard()
        {
            if (Collection.Contains("*"))
            {
                //this is a wildcard importer.
                List<BaseImporter> expanded = new List<BaseImporter>();
                var collection = Collection;
                var regex = collection
                    .Replace(".", "\\.")
                    .Replace("*", "(?<wildcards>.*)");
                var db = GetDatabase();
                var collections = db.ListCollections().ToList();
                foreach (var c in collections)
                {
                    var collectionName = c["name"].AsString;
                    var match = Regex.Match(collectionName, regex);
                    if (match.Success)
                    {
                        var wildcardValue = match.Groups["wildcards"].Value;
                        MongoImporter importer = new MongoImporter()
                        {
                            Connection = this.Connection,
                            Collection = collectionName,
                            EsIndex = this.EsIndex.Replace("*", wildcardValue),
                            EsServer = this.EsServer,
                        };
                        expanded.Add(importer);
                    }

                }
                return expanded;
            }
            else
            {
                return null;
            }
        }

        private IMongoDatabase GetDatabase()
        {
            var url = new MongoUrl(Connection);
            var client = new MongoClient(url);

            var db = client.GetDatabase(url.DatabaseName);
            return db;
        }

        protected override PollResult OnPoll()
        {
            List<BsonDocument> resultList;

            DateTime limitDate = DateTime.Now.AddSeconds(-30);
            FieldDefinition<BsonDocument> ts = "ts";
            resultList = _collection
                .Find(Builders<BsonDocument>.Filter.And(
                        Builders<BsonDocument>.Filter.Gte("ts", _checkpoint.LastCheckpoint),
                        Builders<BsonDocument>.Filter.Lt("ts", limitDate)))
                    .Sort(Builders<BsonDocument>.Sort.Ascending(ts))
                    .Limit(1000)
                    .ToList();
           
             if (resultList.Count == 0) return PollResult.Empty;

            DateTime lastCheckpoint = resultList.Select(b => b["ts"]. AsDateTime).Max(); 
            var ready = resultList.Select(b =>
            {
                try
                {
                    b["_id"] = b["_id"].ToString();

                    //Fix string format to avoid problems with locales.
                    DateTime timestamp = b["ts"].ToUniversalTime();
                    b["ts"] = timestamp.ToString(DateTimeFormatForNestQuery, CultureInfo.InvariantCulture);
                    b["collection"] = Collection;
                    b["mongo-server"] = Connection;
                    b["source"] = Connection + "/" + Collection;
                    var level = b["le"].AsString;
                    String ttl = "30d";
                    if (level == "DEBUG")
                    {
                        ttl = "2d";
                    }
                    else if (level == "INFO")
                    {
                        ttl = "5d";
                    }
                    else if (level == "WARN")
                    {
                        ttl = "15d";
                    }
                    var jsonString = b.ToJson();
                    var replaced = Regex.Replace(jsonString, "CSUUID\\(\"(?<csuid>.+?)\"\\)", "\"${csuid}\"");
                    
                    return (JObject)JsonConvert.DeserializeObject(replaced);
                }
                catch (Exception ex)
                {
                    _log.Error("Error converting mongo log: " + b.ToString(), ex);
                    return null;
                }
            });

            var results = ready.Where(e => e!= null).ToList();

            //var request = new BulkRequest();
            //request.Index = importer.Index;
            //request.Operations = new List<IBulkOperation>();
            //foreach (var result in results)
            //{
            //    var obj = (JObject)JsonConvert.DeserializeObject(result);
            //    var operation = new BulkIndexOperation<JObject>(obj);
            //    operation.Id = obj["_id"].ToString();
            //    request.Operations.Add(new BulkIndexOperation<JObject>(obj));
            //}
            //var retvalue = _client.Bulk(request);

            StringBuilder request = new StringBuilder();
            foreach (var result in results)
            {
                var requestString = String.Format("{{ \"index\" : {{ \"_type\" : \"log\", \"_id\" : \"{0}\"  }} }}", result["_id"].ToString());
                request.AppendLine(requestString);
                request.AppendLine(result.ToString(Formatting.None));
            }
            var response = new PollResult();
            response.FullJsonForElasticBulkEndpoint = request.ToString();
            response.HasMore = results.Count == 1000;
            response.Checkpoint = lastCheckpoint;
            response.Count = results.Count;
            return response;
        }

        public override void SaveCheckpoint(Object checkpoint)
        {
            _checkpoint.LastCheckpoint = (DateTime) checkpoint;
            _checkpointCollection.ReplaceOneAsync(
                x => x.CollectionName == _checkpoint.CollectionName,
                _checkpoint, 
                new UpdateOptions { IsUpsert = true });
        }
    }
}
