namespace Jarvis.Framework.Tests.ProjectionsTests
{
    public class SpyNotifier : INotifyToSubscribers
    {
        public static int Counter = 0;

        public Task Send(object msg)
        {
            Counter++;
            return Task.CompletedTask;
        }
    }
}