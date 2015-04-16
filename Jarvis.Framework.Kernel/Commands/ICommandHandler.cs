using Jarvis.Framework.Kernel.Engine;
using Jarvis.Framework.Shared.Commands;

namespace Jarvis.Framework.Kernel.Commands
{
    public interface ICommandHandler<in T> : ICommandHandler where T : ICommand
    {
        void Handle(T cmd);
    }
}