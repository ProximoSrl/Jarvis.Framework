using Fasterflect;
using Jarvis.Framework.Shared.Events;
using Jarvis.Framework.Shared.IdentitySupport;
using Jarvis.NEventStoreEx.CommonDomainEx;
using Jarvis.Framework.Shared.Helpers;
using NEventStore;
using System.Linq;
using System;

namespace Jarvis.Framework.Kernel.ProjectionEngine.Client
{
    public interface ICommitEnhancer
    {
        void Enhance(ICommit commit);
    }

    /// <summary>
    /// Does a standard enhancement, where some of the property
    /// of the domain event are taken from whole commit information and
    /// from header information of the whole commit.
    /// </summary>
    public class CommitEnhancer : ICommitEnhancer
    {
        private readonly IIdentityConverter _converter;

        public CommitEnhancer(IIdentityConverter converter)
        {
            _converter = converter;
        }

        public void Enhance(ICommit commit)
        {
            Int32 startRevision = commit.StreamRevision - commit.Events.Count + 1;
            //Pay attention, we could have commit that are only stream and does
            //not contains domain events.
            foreach (var eventMessage in commit.Events.Where(m => m.Body is DomainEvent))
            {
                var evt = (DomainEvent)eventMessage.Body;
                var headers = commit.Headers;
                if (eventMessage.Headers.Count > 0)
                {
                    headers = commit.Headers.ToDictionary(k => k.Key, k => k.Value);
                    foreach (var eventHeader in eventMessage.Headers)
                    {
                        headers[eventHeader.Key] = eventHeader.Value;
                    }
                }
                evt.SetPropertyValue(d => d.CommitStamp, commit.CommitStamp);
                evt.SetPropertyValue(d => d.CommitId, commit.CommitId);
                evt.SetPropertyValue(d => d.Version, startRevision++);
                evt.SetPropertyValue(d => d.Context, headers);
                evt.SetPropertyValue(d => d.CheckpointToken, commit.CheckpointToken);
            }
        }
    }

    /// <summary>
    /// A <see cref="ICommitEnhancer"/> implementation that does not
    /// modify the commit.
    /// </summary>
    public class NullCommitEnhancer : ICommitEnhancer
    {
        public void Enhance(ICommit commit)
        {
            // Method intentionally left empty.
        }
    }
}