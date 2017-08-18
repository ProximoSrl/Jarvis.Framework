using Jarvis.Framework.Shared.Helpers;
using Jarvis.Framework.Shared.Messages;
using System.Threading.Tasks;

namespace Jarvis.Framework.Tests.ProjectionsTests
{
    public class SpyNotifier : INotifyToSubscribers
    {
        public static int Counter = 0;

        public Task Send(object msg)
        {
            Counter++;
            return TaskHelpers.CompletedTask;
        }
    }
}