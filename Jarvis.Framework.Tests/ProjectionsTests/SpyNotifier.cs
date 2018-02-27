using Jarvis.Framework.Shared.Helpers;
using Jarvis.Framework.Shared.Messages;
using System.Threading.Tasks;

namespace Jarvis.Framework.Tests.ProjectionsTests
{
    public class SpyNotifier : INotifyToSubscribers
    {
        public int Counter { get; set; } = 0;

        public Task Send(object msg)
        {
            Counter++;
            return Task.CompletedTask;
        }
    }
}