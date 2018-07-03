using Fasterflect;
using Jarvis.Framework.Shared.Events;
using Jarvis.Framework.Shared.Helpers;
using System.Linq;
using Jarvis.Framework.Shared.IdentitySupport;
using NStore.Core.Persistence;
using NStore.Domain;

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
		private readonly IIdentityConverter _converter;

		public CommitEnhancer(IIdentityConverter converter)
		{
			_converter = converter;
		}

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
#pragma warning disable S125 // Sections of code should not be "commented out"
					//if (eventMessage.Headers.Count > 0)
					//{
					//    headers = chunk.Headers.ToDictionary(k => k.Key, k => k.Value);
					//    foreach (var eventHeader in eventMessage.Headers)
					//    {
					//        headers[eventHeader.Key] = eventHeader.Value;
					//    }
					//}
					//evt.SetPropertyValue(d => d.CommitStamp, commit.CommitStamp);
					evt.SetPropertyValue(d => d.CommitId, chunk.OperationId);
					evt.SetPropertyValue(d => d.CommitStamp, commit.GetTimestamp());
#pragma warning restore S125 // Sections of code should not be "commented out"
					evt.SetPropertyValue(d => d.Version, commit.AggregateVersion);
					evt.SetPropertyValue(d => d.Context, headers);
					evt.SetPropertyValue(d => d.CheckpointToken, chunk.Position);
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