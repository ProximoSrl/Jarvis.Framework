using Jarvis.Framework.Shared.Commands;

namespace Jarvis.Framework.Kernel.Engine
{
    public interface ICommandHandler
    {
        void Handle(ICommand command);
    }
}
