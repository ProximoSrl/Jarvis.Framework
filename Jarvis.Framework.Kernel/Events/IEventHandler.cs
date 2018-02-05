using System.Threading.Tasks;

namespace Jarvis.Framework.Kernel.Events
{
    public interface IEventHandler
    {
    }

    /// <summary>
    /// Interface used to handle events, it returns Task because it is now Async
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface IEventHandler<in T> : IEventHandler
    {
        Task On(T e);
    }
}
