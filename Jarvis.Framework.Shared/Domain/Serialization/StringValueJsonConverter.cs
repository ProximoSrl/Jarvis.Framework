using Fasterflect;
using Jarvis.Framework.Shared.Helpers;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;

namespace Jarvis.Framework.Shared.Domain.Serialization
{
    public class StringValueJsonConverter : JsonConverter
    {
        private static ConcurrentDictionary<Type, FastReflectionHelper.ObjectActivator> _activators
            = new ConcurrentDictionary<Type, FastReflectionHelper.ObjectActivator>();

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var id = (string)((StringValue)value);
            writer.WriteValue(id);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.String)
            {
                FastReflectionHelper.ObjectActivator activator;
                if (!_activators.TryGetValue(objectType, out activator))
                {
                    var ctor = objectType.Constructor(new Type[] { typeof(string) });
                    activator = FastReflectionHelper.GetActivator(ctor);
                    _activators[objectType] = activator;
                }

                var typedId = activator(new object[] { Convert.ToString((object)reader.Value) });
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