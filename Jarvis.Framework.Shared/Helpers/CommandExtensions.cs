using Jarvis.Framework.Shared.Commands;
using Jarvis.Framework.Shared.Domain.Serialization;
using Jarvis.Framework.Shared.Events;
using Jarvis.Framework.Shared.Messages;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace Jarvis.Framework.Shared.Helpers
{
    public static class CommandExtensions
    {
        public static readonly JsonSerializerSettings JsonSerializerSettings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.Auto,
            ContractResolver = new MessagesContractResolver(),
            ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor,
            Converters = new List<JsonConverter>()
                {
                    new StringValueJsonConverter()
                }
        };

        public static bool EnableDiagnostics { get; set; }

        static CommandExtensions()
        {
            EnableDiagnostics = true;
        }

        /// <summary>
        /// Special version for date to avoid serializing in json basic value.
        /// </summary>
        /// <param name="command"></param>
        /// <param name="key"></param>
        /// <param name="dateTimeValue"></param>
        public static void SetContextData(this ICommand command, String key, DateTime dateTimeValue)
        {
            command.SetContextData(key, dateTimeValue.ToString("o"));
        }

        public static void SetContextData(this ICommand command, String key, Object objectValue)
        {
            var serialized = JsonConvert.SerializeObject(objectValue, JsonSerializerSettings);
            command.SetContextData(key, serialized);
        }

        public static T GetContextData<T>(this ICommand command, String key)
        {
            var serialized = command.GetContextData(key);
            if (string.IsNullOrEmpty(serialized))
                return default;

            if (TryToDeserializeBasicValues(serialized, out object value))
            {
                return (T)value;
            }

            return JsonConvert.DeserializeObject<T>(serialized, JsonSerializerSettings);
        }

        /// <summary>
        /// When we change default serialization for command header, using basic serialization
        /// for standard object, we need a function that try to deserialize out value from
        /// a standard string.
        /// </summary>
        /// <param name="serialized"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        private static bool TryToDeserializeBasicValues(string serialized, out object value)
        {
            if (DateTime.TryParseExact(serialized, "o", null, System.Globalization.DateTimeStyles.None, out var dateTimeParsedValue))
            {
                value = dateTimeParsedValue;
                return true;
            }
            else if (int.TryParse(serialized, out var intParsedValue))
            {
                value = intParsedValue;
                return true;
            }

            value = default;
            return false;
        }

        /// <summary>
        /// If the command has some complex header, it will be serialized as string,
        /// to avoid serialization mismatch, this method will de-serialize the 
        /// content to the real object.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="value"></param>
        /// <returns></returns>
        public static T DeserializeFromCommandHeader<T>(this String value)
        {
            if (string.IsNullOrEmpty(value))
                return default(T);

            return JsonConvert.DeserializeObject<T>(value, JsonSerializerSettings);
        }

        public static ICommand WithDiagnosticDescription(this ICommand command, string description)
        {
            if (!EnableDiagnostics)
                return command;

            if (description != null)
            {
                command.SetContextData("triggered-by-description", description);
            }
            return command;
        }

        public static ICommand WithDiagnosticTriggeredByInfo(this ICommand command, IMessage message, string description = null)
        {
            if (!EnableDiagnostics)
                return command;

            command.SetContextData("triggered-by", message.GetType().FullName);
            command.SetContextData("triggered-by-id", message.MessageId.ToString());

            var @event = message as DomainEvent;
            if (@event != null)
            {
                command.SetContextData("triggered-by-aggregate", @event.AggregateId.AsString());
            }

            return command.WithDiagnosticDescription(description);
        }

        public static string ExtractUserId(this ICommand command)
        {
            return command.GetContextData(MessagesConstants.UserId);
        }

        public static String ExtractAggregateId(this ICommand command)
        {
            if (command is IAggregateCommand aggregateCommand)
            {
                return aggregateCommand.AggregateId?.AsString();
            }
            return null;
        }
    }
}
