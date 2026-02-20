using Jarvis.Framework.Kernel.Engine;
using Jarvis.Framework.Shared.Commands;
using System.Threading;
using System.Threading.Tasks;

namespace Jarvis.Framework.Kernel.Commands
{
    public interface ICommandHandler<in T> : ICommandHandler where T : ICommand
    {
        Task HandleAsync(T cmd, CancellationToken cancellationToken = default);
    }
}