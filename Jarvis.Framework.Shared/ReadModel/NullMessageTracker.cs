using Jarvis.Framework.Shared.Commands;
using Jarvis.Framework.Shared.Commands.Tracking;
using Jarvis.Framework.Shared.Messages;
using System;

namespace Jarvis.Framework.Shared.ReadModel
{
    public class NullMessageTracker : IMessagesTracker
    {
        public static NullMessageTracker Instance { get; set; }

        static NullMessageTracker()
        {
            Instance = new NullMessageTracker();
        }

        public void Started(IMessage msg)
        {
            // Method intentionally left empty.
        }

        public void Completed(ICommand command, DateTime completedAt)
        {
            // Method intentionally left empty.
        }

        public bool Dispatched(Guid messageId, DateTime dispatchedAt)
        {
            return true;
        }

        public void Drop()
        {
            // Method intentionally left empty.
        }

        public void Failed(ICommand command, DateTime failedAt, Exception ex)
        {
            // Method intentionally left empty.
        }

        public void ElaborationStarted(ICommand command, DateTime startAt)
        {
            // Method intentionally left empty.
        }
    }
}
