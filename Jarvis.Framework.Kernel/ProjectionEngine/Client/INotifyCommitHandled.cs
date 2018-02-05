using Jarvis.Framework.Shared.Helpers;
using NStore.Core.Persistence;
using NStore.Domain;
using System;
using System.Threading.Tasks;

namespace Jarvis.Framework.Kernel.ProjectionEngine.Client
{
	public interface INotifyCommitHandled
    {
		/// <summary>
		/// This signal when the <see cref="ProjectionEngine"/> dispatched
		/// a <see cref="Changeset"/> to all projection of a given slot.
		/// </summary>
		/// <param name="slotName"></param>
		/// <param name="chunk"></param>
		Task SetDispatched(String slotName, IChunk chunk);
    }

    public class NullNotifyCommitHandled : INotifyCommitHandled
    {
        public Task SetDispatched(String slotName, IChunk chunk)
        {
            return Task.CompletedTask;
        }
    }
}