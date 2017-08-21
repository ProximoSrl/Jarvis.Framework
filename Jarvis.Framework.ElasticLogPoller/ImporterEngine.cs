using Elasticsearch.Net;
using Jarvis.Framework.ElasticLogPoller.Importers;
using log4net;
using Nest;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Jarvis.Framework.ElasticLogPoller
{
    public class ImporterEngine
    {
        private static ILog _logger = LogManager.GetLogger(typeof(ImporterEngine));

        private ElasticClient _client;

        public IList<BaseImporter> Importers { get; set; }

        internal void HandleWildcards()
        {
            //need to handle wildcards for mongo collection
            foreach (var importer in Importers.ToList())
            {
                var expandedImportersList = importer.HandleWildcard();
                if (expandedImportersList != null && expandedImportersList.Count > 0)
                {
                    Importers.Remove(importer);
                    foreach (var expandedImporter in expandedImportersList)
                    {
                        Importers.Add(expandedImporter);
                    }
                }
            }
        }

        internal void Configure()
        {
            foreach (var importer in Importers)
            {
                if (_logger.IsDebugEnabled) _logger.DebugFormat("Configure importer {0}", importer.Type);
                importer.Configure();
            }
        }

        internal Boolean Poll()
        {
            Boolean hasMore = false;
            foreach (var importer in Importers)
            {
                if (_logger.IsDebugEnabled) _logger.DebugFormat("Polling importer {0}", importer.Type);
                try
                {
                    var pollResult = importer.Poll();
                    if (pollResult.HasMore) hasMore = true;
                }
                catch (Exception ex)
                {
                    _logger.Error("Error importing logs for poller " + importer.Id, ex);
                    Thread.Sleep(60 * 1000);
                }
            }
            return hasMore;
        }
    }
}
