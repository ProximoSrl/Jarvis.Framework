using System.Threading.Tasks;

namespace Jarvis.Framework.Kernel.ProjectionEngine.Rebuild
{
    /// <summary>
    /// Common interface for rebuild engine.
    /// </summary>
    public interface IRebuildEngine
    {
        /// <summary>
        /// Perform rebuild.
        /// </summary>
        /// <returns></returns>
        Task<RebuildStatus> RebuildAsync();
    }
}