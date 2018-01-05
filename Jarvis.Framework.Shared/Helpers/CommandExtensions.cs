using Jarvis.Framework.Shared.Commands;
using Jarvis.Framework.Shared.Domain.Serialization;
using Jarvis.Framework.Shared.Messages;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace Jarvis.Framework.Shared.Helpers
{
	public static class CommandExtensions
	{
		private static readonly JsonSerializerSettings jsonSerializerSettings = new JsonSerializerSettings
		{
			TypeNameHandling = TypeNameHandling.Auto,
			ContractResolver = new MessagesContractResolver(),
			ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor,
			Converters = new List<JsonConverter>()
				{
					new StringValueJsonConverter()
				}
		};

		public static void SetContextData(this ICommand command, String key, Object objectValue)
		{
			var serialized = JsonConvert.SerializeObject(objectValue, jsonSerializerSettings);
			command.SetContextData(key, serialized);
		}

		public static T GetContextData<T>(this ICommand command, String key)
		{
			var serialized = command.GetContextData(key);
			if (string.IsNullOrEmpty(serialized))
				return default(T);

			return JsonConvert.DeserializeObject<T>(serialized, jsonSerializerSettings);
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

			return JsonConvert.DeserializeObject<T>(value, jsonSerializerSettings);
		}
	}
}
