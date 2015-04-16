using Jarvis.Framework.Shared.Messages;

namespace Jarvis.Framework.Tests.ProjectionsTests
{
    public class SpyNotifier : INotifyToSubscribers
    {
        public static int Counter = 0;
        public void Send(object msg)
        {
            Counter++;
        }
    }
}