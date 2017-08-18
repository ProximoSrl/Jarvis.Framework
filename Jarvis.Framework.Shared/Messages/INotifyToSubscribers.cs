using Jarvis.Framework.Shared.Helpers;
using System.Threading.Tasks;

namespace Jarvis.Framework.Shared.Messages
{
    public interface INotifyToSubscribers
    {
        Task Send(object msg);
    }

    public sealed class NotifyToNobody : INotifyToSubscribers
    {
        public Task Send(object msg)
        {
            return TaskHelpers.CompletedTask;
        }
    }
}