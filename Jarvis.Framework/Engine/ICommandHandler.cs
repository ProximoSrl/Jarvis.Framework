using Jarvis.Framework.Shared.Commands;
using System.Threading.Tasks;

namespace Jarvis.Framework.Kernel.Engine
{
    public interface ICommandHandler
    {
        Task HandleAsync(ICommand command);

        /// <summary>
        /// Called when we have a concurrency exception and we need to reuse the
        /// handler to process again the command.
        /// </summary>
        /// <returns></returns>
        Task ClearAsync();
    }
}
