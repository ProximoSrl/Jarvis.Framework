using System.Threading.Tasks;

namespace Jarvis.Framework.Shared.Messages
{
    /// <summary>
    /// <para>Send a notification to some part of the software interested in something happened.</para>
    /// <para>This is primarly used to send notification of readmodel updates.</para>
    /// </summary>
    public interface INotifyToSubscribers
    {
        Task Send(object msg);
    }

    /// <summary>
    /// This is used to avoid sending notification to any subscriber.
    /// This notification send everything to null.
    /// </summary>
    public sealed class NotifyToNobody : INotifyToSubscribers
    {
        public Task Send(object msg)
        {
            return Task.CompletedTask;
        }
    }
}