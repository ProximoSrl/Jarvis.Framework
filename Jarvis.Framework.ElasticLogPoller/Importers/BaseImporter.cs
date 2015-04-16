
using log4net;
using Nest;
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

            var settings = new ConnectionSettings(
                node,
                defaultIndex: "logs"
            );

            Client = new ElasticClient(settings);
        }

        public abstract void SaveCheckpoint(Object checkpoint);

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
                }
                SaveCheckpoint(pollResult.Checkpoint);
            }
            return pollResult;

        }

        protected abstract PollResult OnPoll();

        protected void SetTtl(String type, String defaultDuration)
        {
            if (!Client.IndexExists(x => x.Index(EsIndex)).Exists)
            {
                _log.Info("Create ES index");
                var response = Client.CreateIndex(EsIndex, id => id.NumberOfShards(2));
            }
            var indexDefinition = new RootObjectMapping
            {
                Properties = new Dictionary<PropertyNameMarker, IElasticType>(),
                Name = "log",
            };

            var ttlDescriptor = new TtlFieldMappingDescriptor();
            ttlDescriptor.Enable(true);
            ttlDescriptor.Default(defaultDuration);
            indexDefinition.TtlFieldMappingDescriptor = ttlDescriptor;

            var notAnalyzedProperty = new StringMapping
            {
                Index = FieldIndexOption.NotAnalyzed
            };

            //property.Fields.Add("le", property);
            indexDefinition.Properties.Add("le", notAnalyzedProperty);
            indexDefinition.Properties.Add("us", notAnalyzedProperty);
            indexDefinition.Properties.Add("lo", notAnalyzedProperty);
            indexDefinition.Properties.Add("do", notAnalyzedProperty);
            indexDefinition.Properties.Add("ma", notAnalyzedProperty);
            indexDefinition.Properties.Add("pn", notAnalyzedProperty);
            indexDefinition.Properties.Add("ln", notAnalyzedProperty);
            indexDefinition.Properties.Add("cn", notAnalyzedProperty);

            indexDefinition.Properties.Add("mongo-server", notAnalyzedProperty);
            indexDefinition.Properties.Add("collection", notAnalyzedProperty);
            indexDefinition.Properties.Add("source", notAnalyzedProperty);
            Client.Map<object>(x => x
                .InitializeUsing(indexDefinition)
                .Type("log")
            );
            _log.Info("ES Mapping set");
        }
    }
}
