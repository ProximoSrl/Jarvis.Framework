using Fasterflect;
using Jarvis.Framework.Kernel.ProjectionEngine.Client;
using Jarvis.Framework.Shared.Events;
using Jarvis.Framework.Shared.Helpers;
using Jarvis.Framework.Shared.Messages;
using Jarvis.Framework.Tests.EngineTests;
using NStore.Core.Persistence;
using NStore.Domain;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Jarvis.Framework.Tests.Kernel.Support
{

    public class TestChunk : IChunk
    {
        public TestChunk(long position, string partitionId, long index, object payload, Guid operationId)
        {
            OperationId = operationId.ToString();
            Position = position;
            PartitionId = partitionId;
            Index = index;
            Payload = payload;
        }

        public long Position { get; set; }

        public string PartitionId { get; set; }

        public long Index { get; set; }

        public object Payload { get; set; }

        public string OperationId { get; set; }

        public DomainEvent[] DomainEvents
        {
            get
            {
                if (Payload is Changeset cs)
                    return cs.Events.OfType<DomainEvent>().ToArray();
                return Array.Empty<DomainEvent>();
            }
        }
    }
}
