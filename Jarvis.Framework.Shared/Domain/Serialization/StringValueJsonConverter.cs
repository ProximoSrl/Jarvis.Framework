using Fasterflect;
using Jarvis.Framework.Shared.Helpers;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;

namespace Jarvis.Framework.Shared.Domain.Serialization
{
    public class StringValueJsonConverter : JsonConverter
    {
        private static readonly ConcurrentDictionary<Type, Func<string, object>> _activators
            = new ConcurrentDictionary<Type, Func<string, object>>();

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var id = (string)((StringValue)value);
            writer.WriteValue(id);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.String)
            {
                Func<string, object> activator;
                if (!_activators.TryGetValue(objectType, out activator))
                {
                    var ctor = objectType.Constructor(new Type[] { typeof(string) });
                    activator = FastReflectionHelper.GetActivator<string, object>(ctor);
                    _activators[objectType] = activator;
                }

                var typedId = activator(Convert.ToString((object)reader.Value));
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