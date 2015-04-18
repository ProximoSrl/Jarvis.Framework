using Jarvis.Framework.Kernel.Engine;

namespace Jarvis.Framework.Tests.EngineTests.TokenTests
{
    public class FileAggregateState : AggregateState
    {
        public static GrantName LockGrant = new GrantName("file-lock");
        public bool     IsLocked { get; private set; }

        private void When(FileLocked e)
        {
            IsLocked = true;
            AddGrant(LockGrant, new Token(e.MessageId.ToString()));
        }

        private void When(FileUnLocked e)
        {
            IsLocked = false;
        }
    }
}