using Fasterflect;
using Jarvis.Framework.Shared.Events;
using Jarvis.Framework.Shared.Helpers;
using NStore.Core.Persistence;
using NStore.Domain;
using System.Collections.Generic;
using System.Linq;

namespace Jarvis.Framework.Kernel.ProjectionEngine.Client
{
    /// <summary>
    /// Some of the information that are contained in Domain Events is contained
    /// in header of the <see cref="Changeset" />. Those information sholuld be copied
    /// in special field of <see cref="DomainEvent"/>, this interface abstract the component
    /// that is doing that specific operation.
    /// </summary>
    public interface ICommitEnhancer
    {
        void Enhance(IChunk chunk);
    }

    /// <summary>
    /// Does a standard enhancement, where some of the property
    /// of the domain event are taken from whole <see cref="Changeset"/> information and
    /// from header information of the whole <see cref="Changeset"/>.
    /// </summary>
    public class CommitEnhancer : ICommitEnhancer
    {
        public void Enhance(IChunk chunk)
        {
            if (chunk.Payload is Changeset commit)
            {
                DomainEvent evt = null;
                int eventPosition = 1;
                var headers = commit.Headers;

                // remove command, it just waste space for serialization
                if (headers.ContainsKey(ChangesetCommonHeaders.Command))
                {
                    headers = new Dictionary<string, object>(headers.Count - 1);
                    foreach (var kvp in commit.Headers)
                    {
                        if (kvp.Key != ChangesetCommonHeaders.Command)
                        {
                            headers.Add(kvp.Key, kvp.Value);
                        }
                    }
                }

                foreach (var eventMessage in commit.Events.Where(m => m is DomainEvent))
                {
                    evt = (DomainEvent)eventMessage;
                    evt.CommitId = chunk.OperationId;

                    evt.Version = commit.AggregateVersion;
                    evt.Context = headers;
                    evt.CheckpointToken = chunk.Position;
                    evt.EventPosition = eventPosition++;
                }

                evt?.SetPropertyValue(d => d.IsLastEventOfCommit, true);
            }
        }
    }

    /// <summary>
    /// A <see cref="ICommitEnhancer"/> implementation that does not
    /// modify the commit.
    /// </summary>
    public class NullCommitEnhancer : ICommitEnhancer
    {
        public void Enhance(IChunk chunk)
        {
            // Method intentionally left empty.
        }
    }
}