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

    public class CommitEnhancer : ICommitEnhancer
    {
        readonly IIdentityConverter _converter;

        public CommitEnhancer(IIdentityConverter converter)
        {
            _converter = converter;
        }

        public void Enhance(ICommit commit)
        {
            var esid = (EventStoreIdentity)_converter.ToIdentity(commit.StreamId);
            foreach (var eventMessage in commit.Events)
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
                //evt.SetPropertyValue(d => d.AggregateId, esid);
                evt.SetPropertyValue(d => d.Version, commit.StreamRevision);
                evt.SetPropertyValue(d => d.Context, headers);
                evt.SetPropertyValue(d => d.CheckpointToken, commit.CheckpointToken);
            }
        }
    }

    public class NullCommitEnhancer : ICommitEnhancer
    {
        public void Enhance(ICommit commit)
        {
            
        }
    }
}