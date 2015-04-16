namespace Jarvis.Framework.Shared.Messages
{
    public interface INotifyToSubscribers
    {
        void Send(object msg);
    }

    public sealed class NotifyToNobody : INotifyToSubscribers
    {
        public void Send(object msg)
        {
            
        }
    }
}