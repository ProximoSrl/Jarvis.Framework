using System;

namespace Jarvis.Framework.Shared.Messages
{
    public interface IMessage
    {
        Guid MessageId { get; }
        string Describe();
    }
}