using NEventStore;

namespace Jarvis.Framework.Kernel.ProjectionEngine.Client
{
    public interface INotifyCommitHandled
    {
        void SetDispatched(ICommit commit);
    }

    public class NullNotifyCommitHandled : INotifyCommitHandled
    {
        public void SetDispatched(ICommit commit)
        {

        }
    }
}