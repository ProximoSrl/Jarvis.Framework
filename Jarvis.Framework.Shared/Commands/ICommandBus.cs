using System;
using System.Threading.Tasks;

namespace Jarvis.Framework.Shared.Commands
{
    public interface ICommandBus
    {
		/// <summary>
		/// Invia un comando
		/// </summary>
		/// <param name="command">comando</param>
		/// <param name="impersonatingUser">utente di contesto</param>
		/// <returns>comando</returns>
		Task<ICommand> SendAsync(ICommand command, string impersonatingUser = null);

        /// <summary>
        /// Schedula l'invio di un comando
        /// </summary>
        /// <param name="delay">ritardo</param>
        /// <param name="command">comando</param>
        /// <param name="impersonatingUser">utente di contesto</param>
        /// <returns>comando</returns>
        Task<ICommand> DeferAsync(TimeSpan delay, ICommand command, string impersonatingUser = null);

        /// <summary>
        /// Invia un comando sulla stessa coda del bus
        /// </summary>
        /// <param name="command">comando </param>
        /// <param name="impersonatingUser">utente </param>
        /// <returns></returns>
        Task<ICommand> SendLocalAsync(ICommand command, string impersonatingUser = null);
    }
}
