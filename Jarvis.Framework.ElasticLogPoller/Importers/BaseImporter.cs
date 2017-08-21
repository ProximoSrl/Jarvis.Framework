
using Elasticsearch.Net;
using log4net;
using Nest;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Jarvis.Framework.ElasticLogPoller.Importers
{
    public abstract class BaseImporter
    {
        protected ILog _log;

        public BaseImporter()
        {
            _log = LogManager.GetLogger(this.GetType());
        }

        public abstract String Type { get; }

        public abstract String Id { get;  }

        /// <summary>
        /// Destination ES index 
        /// </summary>
        public String EsIndex { get; set; }

        public String EsServer { get; set; }

        protected ElasticClient Client;

        private String _baseUrl;

        public virtual void Configure()
        {
            _baseUrl = EsServer.TrimEnd('/') + "/";
            var node = new Uri(EsServer);

            var pool = new StaticConnectionPool(new[] { node });
            var settings = new ConnectionSettings(pool)
                .DisableDirectStreaming()
                .PrettyJson()
                .DefaultIndex("logs");

            Client = new ElasticClient(settings);
        }

        public abstract void SaveCheckpoint(Object checkpoint);

        public virtual List<BaseImporter> HandleWildcard()
        {
            return null;
        }

        public PollResult Poll()
        {
            var pollResult = OnPoll();
            _log.DebugFormat("Polling {0} returned :{1} logs", Id, pollResult.Count);
            if (!String.IsNullOrEmpty(pollResult.FullJsonForElasticBulkEndpoint))
            {
                using (var wc = new WebClient())
                {
                    var result = wc.UploadString(
                        _baseUrl + EsIndex + "/_bulk",
                        pollResult.FullJsonForElasticBulkEndpoint);

                    var deserialized = (JObject)JsonConvert.DeserializeObject(result);
                    if (deserialized["errors"].Value<Boolean>() == true)
                    {
                        throw new Exception($"Unable to index block of logs: {result}");
                    }
                }
                SaveCheckpoint(pollResult.Checkpoint);
            }
            return pollResult;
        }

        protected abstract PollResult OnPoll();

        protected void SetTtl(String type, String defaultDuration)
        {
            if (!Client.IndexExists(EsIndex).Exists)
            {
                _log.Info("Create ES index: " + EsIndex);
                var response = Client.CreateIndex(EsIndex, id => id
                    .Settings(s => s.NumberOfShards(2)));
                if (!response.IsValid)
                    throw new Exception($"Unable to create index {EsIndex} - {response.DebugInformation}");

                var mapResponse = Client.Map<EsLog>(m => m
                    .AutoMap()
                    .TtlField(ttl => ttl.Enable(true))
                    .AllField(all => all.Enabled(false)));

                if (!mapResponse.IsValid)
                    throw new Exception($"Unable to map object in index {EsIndex} - {mapResponse.DebugInformation}");
            }
        }
    }
}
