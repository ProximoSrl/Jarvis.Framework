using System;
using System.Linq;
using Jarvis.Framework.Shared.ReadModel;

namespace Jarvis.Framework.Shared.Messages
{
	/// <summary>
	/// This attribute states that if message is not received in 30
	/// seconds it should be ignored, because it is not necessary anymore.
	/// </summary>
	[TimeToBeReceived("00:00:30")]
	public class ReadModelUpdatedMessage : IMessage
	{
		public enum UpdateAction
		{
			Created,
			Deleted,
			Updated
		}

		public UpdateAction Action { get; set; }
		public object Id { get; set; }
		public object ReadModel { get; set; }
		public string ModelName { get; set; }
		public string[] Topics { get; set; }

		public Guid MessageId { get; private set; }

		public ReadModelUpdatedMessage()
		{
			MessageId = Guid.NewGuid();
		}

		private static string GetModelName<T, TKey>() where T : IReadModelEx<TKey>
		{
			var modelName = typeof(T).Name;
			if (modelName.EndsWith("ReadModel"))
				modelName = modelName.Remove(modelName.Length - "ReadModel".Length);

			return modelName;
		}

		static ReadModelUpdatedMessage Enhance(ReadModelUpdatedMessage message, string[] topics = null)
		{
			if (topics != null)
			{
				message.Topics = topics;
				return message;
			}

			if (message.ReadModel is ITopicsProvider)
			{
				message.Topics = ((ITopicsProvider)message.ReadModel).GetTopics().ToArray();
			}

			return message;
		}

		public static ReadModelUpdatedMessage Created<T, TKey>(T document) where T : IReadModelEx<TKey>
		{
			return ReadModelUpdatedMessage.Created<T, TKey>(document.Id, document);
		}

		public static ReadModelUpdatedMessage Updated<T, TKey>(T document) where T : IReadModelEx<TKey>
		{
			return ReadModelUpdatedMessage.Updated<T, TKey>(document.Id, document);
		}

		public static ReadModelUpdatedMessage Created<T, TKey>(TKey id, object document) where T : IReadModelEx<TKey>
		{
			return Enhance(new ReadModelUpdatedMessage()
			{
				Action = UpdateAction.Created,
				Id = id,
				ReadModel = document,
				ModelName = GetModelName<T, TKey>()
			});
		}

		public static ReadModelUpdatedMessage Updated<T, TKey>(TKey id, object document) where T : IReadModelEx<TKey>
		{
			return Enhance(new ReadModelUpdatedMessage()
			{
				Action = UpdateAction.Updated,
				Id = id,
				ReadModel = document,
				ModelName = GetModelName<T, TKey>()
			});
		}

		public static ReadModelUpdatedMessage Deleted<T, TKey>(TKey id, string[] topics = null) where T : IReadModelEx<TKey>
		{
			return Enhance(new ReadModelUpdatedMessage()
			{
				Action = UpdateAction.Deleted,
				Id = id,
				ModelName = GetModelName<T, TKey>()
			}, topics);
		}

		public string Describe()
		{
			return $"Updated readmodel {ReadModel} of {Id}";
		}
	}
}
