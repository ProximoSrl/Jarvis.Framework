using System.Threading.Tasks;

namespace Jarvis.Framework.Kernel.Events
{
    public interface IObserveProjection
    {
        Task RebuildStartedAsync();

        /// <summary>
        /// During rebuild ended we can flush or do some database related activities
        /// that could benefit for being async
        /// </summary>
        /// <returns></returns>
        Task RebuildEndedAsync();
    }
}
