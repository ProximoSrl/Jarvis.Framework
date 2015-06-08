using System;
using MongoDB.Bson.Serialization.Attributes;

namespace Jarvis.MonitoringAgentServer.Server.Data
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
