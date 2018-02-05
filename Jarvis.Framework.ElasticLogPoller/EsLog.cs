using Nest;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jarvis.Framework.ElasticLogPoller
{
    [ElasticsearchType(Name = "eslog", IdProperty = "iId")]
    public class EsLog
    {
        [String(Index = FieldIndexOption.NotAnalyzed)]
        public String Id { get; set; }

        [String(Index = FieldIndexOption.NotAnalyzed)]
        public String Le { get; set; }

        [String(Index = FieldIndexOption.NotAnalyzed)]
        public String Us { get; set; }

        [String(Index = FieldIndexOption.NotAnalyzed)]
        public String Lo { get; set; }

        [String(Index = FieldIndexOption.NotAnalyzed)]
        public String Do { get; set; }

        [String(Index = FieldIndexOption.NotAnalyzed)]
        public String Ma { get; set; }

        [String(Index = FieldIndexOption.NotAnalyzed)]
        public String Pn { get; set; }

        [String(Index = FieldIndexOption.NotAnalyzed)]
        public String Ln { get; set; }

        [String(Index = FieldIndexOption.NotAnalyzed)]
        public String Cn { get; set; }

        [Date(Index = NonStringIndexOption.NotAnalyzed)]
        public DateTime Ts { get; set; }
    }
}
