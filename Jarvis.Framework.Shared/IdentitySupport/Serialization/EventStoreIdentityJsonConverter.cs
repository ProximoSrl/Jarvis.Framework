using Newtonsoft.Json;
using System;

namespace Jarvis.Framework.Shared.IdentitySupport.Serialization
{
    public class EventStoreIdentityJsonConverter : JsonConverter
    {
        private readonly IIdentityConverter _identityConverter;

        public EventStoreIdentityJsonConverter(IIdentityConverter identityConverter)
        {
            _identityConverter = identityConverter;
        }

        public override bool CanConvert(Type objectType)
        {
            return typeof(IIdentity).IsAssignableFrom(objectType);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var strValue = reader.Value as string;
            return _identityConverter.ToIdentity(strValue);
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value == null)
            {
                writer.WriteNull();
            }
            var id = (IIdentity) value;
            writer.WriteValue(id.AsString());
        }
    }
}
