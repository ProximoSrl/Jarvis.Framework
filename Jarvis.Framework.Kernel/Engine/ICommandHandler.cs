using Jarvis.Framework.Shared.Commands;
using System.Threading.Tasks;

namespace Jarvis.Framework.Kernel.Engine
{
    public interface ICommandHandler
    {
        Task HandleAsync(ICommand command);
    }
}
