using Jarvis.Framework.Kernel.Engine;

namespace Jarvis.Framework.Tests.EngineTests.TokenTests
{
    public class FileAggregateState : AggregateState
    {
        public bool     IsLocked { get; private set; }

        private void When(FileLocked e)
        {
            IsLocked = true;
            AddGrant(new GrantName("file-lock"), new Token(e.MessageId.ToString()));
        }

        private void When(FileUnLocked e)
        {
            IsLocked = false;
        }
    }
}