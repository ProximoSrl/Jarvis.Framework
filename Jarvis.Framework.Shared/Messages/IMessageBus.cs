using System;
using System.Threading.Tasks;

namespace Jarvis.Framework.Shared.Messages
{
    public interface IMessageBus
    {
        /// <summary>
        /// Invia un comando
        /// </summary>
        /// <param name="message">message</param>
        /// <returns>message</returns>
        Task<IMessage> SendAsync(IMessage message);

        /// <summary>
        /// Schedula l'invio di un comando
        /// </summary>
        /// <param name="delay">ritardo</param>
        /// <param name="message">message</param>
        /// <returns>message</returns>
        Task<IMessage> DeferAsync(TimeSpan delay, IMessage message);

        /// <summary>
        /// Invia un comando sulla stessa coda del bus
        /// </summary>
        /// <param name="message">message </param>
        /// <returns></returns>
        Task<IMessage> SendLocalAsync(IMessage message);
    }
}
