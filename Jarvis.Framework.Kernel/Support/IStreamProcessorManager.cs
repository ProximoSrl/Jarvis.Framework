﻿using Fasterflect;
using NStore.Core.Persistence;
using NStore.Core.Processing;
using NStore.Core.Streams;
using NStore.Domain;
using System;
using System.Threading;
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

    /// <summary>
    /// Needed to create <see cref="StreamProcessorManager"/> for a specific
    /// <see cref="IPersistence"/> persitence.
    /// </summary>
    [Obsolete("Should not use it anymore, it creates confusion for registrations.")]
    public interface IStreamProcessorManagerFactory
    {
        IStreamProcessorManager Create(IPersistence persistence);
    }

    public class StreamProcessorManager : IStreamProcessorManager
    {
        private readonly StreamsFactory _streams;

        public StreamProcessorManager(IPersistence persistence)
        {
            _streams = new StreamsFactory(persistence);
        }

        public Task<T> ProcessAsync<T>(String streamId, int versionUpTo)
            where T : class, new()
        {
            var stream = _streams.Open(streamId);
            return stream
                .Aggregate()
                .ToIndex(versionUpTo)
                .RunAsync<T>(StreamProcessorManagerPayloadProcessor.Instance, CancellationToken.None);
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
            if (payload == null)
                return false;

            Changeset changeset = payload as Changeset;
            if (changeset == null)
            {
                //This happens for StreamProcessing, we have not a changesets with events, we have only a simple payload
                CallProcessMethod(state, payload);
            }
            else
            {
                foreach (var evt in changeset.Events)
                {
                    CallProcessMethod(state, evt);
                }
            }

            return true;
        }

        private void CallProcessMethod(object state, object evt)
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
    }
}
