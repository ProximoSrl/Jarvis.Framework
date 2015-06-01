using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Builders;
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

namespace Jarvis.Framework.ElasticLogPoller.Importers
{

    public class MongoImporter : BaseImporter
    {
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

        private MongoCollection<BsonDocument> _collection;
        private MongoCollection<ImportCheckpoint> _checkpointCollection;
        private ImportCheckpoint _checkpoint;
        public override void Configure()
        {
            base.Configure();
            SetTtl("log", "30d");
            var url = new MongoUrl(Connection);
            var client = new MongoClient();

            var db = client.GetServer().GetDatabase(url.DatabaseName);
            _collection = db.GetCollection(Collection);
            _checkpointCollection = db.GetCollection<ImportCheckpoint>("importer.checkpoints");
            _checkpoint = _checkpointCollection.AsQueryable()
                .SingleOrDefault(d => d.CollectionName == Collection);
            if (_checkpoint == null)
            {
                _checkpoint = new ImportCheckpoint() { CollectionName = Collection, LastCheckpoint = DateTime.MinValue };
            }
        }

        protected override PollResult OnPoll()
        {
            List<BsonDocument> resultList;

            resultList = _collection
                .Find(Query.And(
                        Query.GTE("ts", _checkpoint.LastCheckpoint),
                        Query.LT("ts", DateTime.Now.AddSeconds(-30))))
                    .SetSortOrder(SortBy.Ascending("ts"))
                    .Take(1000)
                    .ToList();
           
             if (resultList.Count == 0) return PollResult.Empty;

            DateTime lastCheckpoint = resultList.Select(b => b["ts"]. AsDateTime).Max(); 
            var ready = resultList.Select(b =>
            {
                try
                {
                    b["_id"] = b["_id"].ToString();
                    b["ts"] = b["ts"].ToString();
                    b["collection"] = Collection;
                    b["mongo-server"] = Connection;
                    b["source"] = Connection + "/" + Collection;
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
            _checkpointCollection.Save(_checkpoint);
        }
    }
}
