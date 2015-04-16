using System;
using Newtonsoft.Json;

namespace Jarvis.Framework.Shared.Domain.Serialization
{
    public class StringValueJsonConverter : JsonConverter
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var id = (string)((StringValue)value);
            writer.WriteValue(id);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.String)
            {
                var typedId = Activator.CreateInstance(objectType, new object[] {Convert.ToString((object) reader.Value)});
                return typedId;
            }
            return null;
        }

        public override bool CanConvert(Type objectType)
        {
            return typeof(StringValue).IsAssignableFrom(objectType);
        }
    }
}