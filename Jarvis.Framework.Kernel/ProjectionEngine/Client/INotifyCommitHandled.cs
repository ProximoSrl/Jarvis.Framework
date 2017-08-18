using Jarvis.Framework.Shared.Helpers;
using NEventStore;
using System;
using System.Threading.Tasks;

namespace Jarvis.Framework.Kernel.ProjectionEngine.Client
{
    public interface INotifyCommitHandled
    {
        /// <summary>
        /// This signal when the <see cref="ProjectionEngine"/> dispatched
        /// a <see cref="ICommit"/> to all projection of a given slot.
        /// </summary>
        /// <param name="slotName"></param>
        /// <param name="commit"></param>
        Task SetDispatched(String slotName, ICommit commit);
    }

    public class NullNotifyCommitHandled : INotifyCommitHandled
    {
        public Task SetDispatched(String slotName, ICommit commit)
        {
            // Method intentionally left empty.
        }
    }
}