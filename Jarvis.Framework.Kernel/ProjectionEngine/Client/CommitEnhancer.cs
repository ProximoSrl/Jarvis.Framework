using Fasterflect;
using Jarvis.Framework.Shared.Events;
using Jarvis.Framework.Shared.Helpers;
using System.Linq;
using Jarvis.Framework.Shared.IdentitySupport;
using NStore.Core.Persistence;
using NStore.Domain;
using System;

namespace Jarvis.Framework.Kernel.ProjectionEngine.Client
{
	public interface ICommitEnhancer
	{
		void Enhance(IChunk chunk);
	}

	/// <summary>
	/// Does a standard enhancement, where some of the property
	/// of the domain event are taken from whole commit information and
	/// from header information of the whole commit.
	/// </summary>
	public class CommitEnhancer : ICommitEnhancer
	{
		public void Enhance(IChunk chunk)
		{
			Changeset commit;
			if ((commit = chunk.Payload as Changeset) != null)
			{
                DomainEvent evt = null;
				foreach (var eventMessage in commit.Events.Where(m => m is DomainEvent))
				{
                    evt = (DomainEvent)eventMessage;
					var headers = commit.Headers;
                    evt.CommitId = chunk.OperationId;
					evt.CommitStamp= commit.GetTimestamp();
					evt.Version= commit.AggregateVersion;
					evt.Context= headers;
					evt.CheckpointToken= chunk.Position;
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