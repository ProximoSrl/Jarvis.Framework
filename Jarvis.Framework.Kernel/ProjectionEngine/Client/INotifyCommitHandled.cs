using NEventStore;

namespace Jarvis.Framework.Kernel.ProjectionEngine.Client
{
    public interface INotifyCommitHandled
    {
        /// <summary>
        /// TODO: This should be refactored, because this set the commit as 
        /// dispatched when the first slot process the commit, it should 
        /// contain at lest slot Name.
        /// </summary>
        /// <param name="commit"></param>
        void SetDispatched(ICommit commit);
    }

    public class NullNotifyCommitHandled : INotifyCommitHandled
    {
        public void SetDispatched(ICommit commit)
        {

        }
    }
}