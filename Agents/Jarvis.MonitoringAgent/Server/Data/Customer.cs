using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jarvis.MonitoringAgent.Server.Data
{
    public class Customer
    {

        [BsonId]
        public String Name { get; set; }

        public String PrivateKey { get; set; }

        public String PublicKey { get; set; }

        public Boolean Enabled { get; set; }
    }
}
