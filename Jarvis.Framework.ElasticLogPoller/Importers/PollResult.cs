using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jarvis.Framework.ElasticLogPoller.Importers
{
    public class PollResult
    {
        public Boolean HasMore { get; set; }

        public String FullJsonForElasticBulkEndpoint { get; set; }

        public Object Checkpoint { get; set; }

        public Int32 Count { get; set; }

        public static PollResult Empty { get; private set; }

        static PollResult() 
        {
            Empty = new PollResult(); 
        }


    }
}
