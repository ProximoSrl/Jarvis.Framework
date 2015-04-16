using System.Diagnostics;
using Jarvis.Framework.Shared.Messages;
using Newtonsoft.Json;

namespace Jarvis.Framework.TestHelpers
{

    public static class SerializationHelper
    {
        public static object Rountrip(object source)
        {
            var json = Serialize(source);
            return Deserialize(json);
        }

        public static string Serialize(object dto)
        {
            var serialized = JsonConvert.SerializeObject(
                dto,
                new JsonSerializerSettings()
                    {
                        TypeNameHandling = TypeNameHandling.All,
                        Formatting = Formatting.Indented
                    });
            Debug.WriteLine((string) serialized);

            return serialized;

        }

        public static object Deserialize(string json)
        {
            var deserialized = JsonConvert.DeserializeObject(json, new JsonSerializerSettings()
                {
                    TypeNameHandling = TypeNameHandling.All,
                    ContractResolver = new MessagesContractResolver()
                });

            return deserialized;
        }
    }
}