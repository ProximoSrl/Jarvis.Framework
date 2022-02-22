using Fasterflect;
using Jarvis.Framework.Shared.Events;
using Jarvis.Framework.Shared.Helpers;
using Jarvis.Framework.Shared.Messages;
using NStore.Core.Persistence;
using NStore.Domain;
using System;
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
                foreach (var eventMessage in commit.Events.Where(m => m is DomainEvent))
                {
                    evt = (DomainEvent)eventMessage;
                    var headers = commit.Headers;
                    evt.CommitId = chunk.OperationId;

                    evt.CommitStamp = GetCommitStamp(commit);

                    evt.Version = commit.AggregateVersion;
                    evt.Context = headers;
                    evt.CheckpointToken = chunk.Position;
                }

                evt?.SetPropertyValue(d => d.IsLastEventOfCommit, true);
            }
        }

        private DateTime GetCommitStamp(Changeset changeset)
        {
            if (changeset.Headers.TryGetValue(MessagesConstants.OverrideCommitTimestamp, out var timestamp))
            {
                // I have ovveride timestamp
                if (timestamp is DateTime date)
                {
                    return date;
                }
                else if (timestamp is string dateString)
                {
                    if (DateTime.TryParse(dateString, out var parsedDate))
                    {
                        return parsedDate;
                    }
                }
            }
            return changeset.GetTimestamp();
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