using Fasterflect;
using Jarvis.Framework.Kernel.Engine;
using Jarvis.Framework.Shared.IdentitySupport;
using NStore.Core.Persistence;
using NStore.Core.Processing;
using NStore.Core.Snapshots;
using NStore.Core.Streams;
using NStore.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jarvis.Framework.Kernel.Support
{
	/// <summary>
	/// Simple helper to simply unfolding.
	/// </summary>
	public interface IStreamProcessorManager
	{
		Task<T> ProcessAsync<T>(String streamId, Int32 versionUpTo) where T : class, new();
	}

	public class StreamProcessorManager : IStreamProcessorManager
	{
		private readonly IPersistence _persistence;
		private readonly ISnapshotStore _snapshotStore;
		private readonly StreamsFactory _streams;
		public StreamProcessorManager(IPersistence persistence, ISnapshotStore snapshotStore)
		{
			_persistence = persistence;
			_snapshotStore = snapshotStore;
			_streams = new StreamsFactory(persistence);
		}

		public async Task<T> ProcessAsync<T>(String streamId, int versionUpTo)
			where T : class, new()
		{
			var stream = _streams.Open(streamId);
			return await stream
				.Fold(StreamProcessorManagerPayloadProcessor.Instance)
				.ToIndex(versionUpTo)
				.RunAsync<T>()
				.ConfigureAwait(false);
		}
	}

	public sealed class StreamProcessorManagerPayloadProcessor : IPayloadProcessor
	{
		public static readonly IPayloadProcessor Instance = new StreamProcessorManagerPayloadProcessor();

		private StreamProcessorManagerPayloadProcessor()
		{
		}

		private readonly string[] _methods = { "When", "On" };

		public object Process(object state, object payload)
		{
			Changeset changeset = payload as Changeset;

			if (changeset == null)
			{
				//TODO: Log, this is an error.
				return false;
			}

			foreach (var evt in changeset.Events)
			{
				foreach (var methodName in _methods)
				{
					var method = state.GetType().Method(methodName, new[] { evt.GetType() }, Flags.InstanceAnyVisibility);
					if (method != null)
					{
						method.Call(state, new object[] { evt });
						break; //skip method, go to the next event.
					}
				}
			}

			return true;
		}
	}
}
