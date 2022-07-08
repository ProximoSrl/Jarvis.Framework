using System.Threading.Tasks;

namespace Jarvis.Framework.Kernel.ProjectionEngine
{
    public interface IInitializeReadModelDb
    {
        Task InitAsync(bool drop);
    }
}