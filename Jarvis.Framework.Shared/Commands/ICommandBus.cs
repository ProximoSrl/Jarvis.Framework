using System;
using System.Threading;
using System.Threading.Tasks;

namespace Jarvis.Framework.Shared.Commands
{
    /// <summary>
    /// Interface for a bus capable to send command to a well known executor. Caller
    /// process always known the destination of the command.
    /// </summary>
    public interface ICommandBus
    {
        /// <summary>
        /// Invia un comando
        /// </summary>
        /// <param name="command">comando</param>
        /// <param name="impersonatingUser">utente di contesto</param>
        /// <param name="cancellationToken">cancellation token</param>
        /// <returns>comando</returns>
        Task<ICommand> SendAsync(ICommand command, string impersonatingUser = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Schedula l'invio di un comando
        /// </summary>
        /// <param name="delay">ritardo</param>
        /// <param name="command">comando</param>
        /// <param name="impersonatingUser">utente di contesto</param>
        /// <param name="cancellationToken">cancellation token</param>
        /// <returns>comando</returns>
        Task<ICommand> DeferAsync(TimeSpan delay, ICommand command, string impersonatingUser = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Invia un comando sulla stessa coda del bus
        /// </summary>
        /// <param name="command">comando </param>
        /// <param name="impersonatingUser">utente </param>
        /// <param name="cancellationToken">cancellation token</param>
        /// <returns></returns>
        Task<ICommand> SendLocalAsync(ICommand command, string impersonatingUser = null, CancellationToken cancellationToken = default);
    }
}
