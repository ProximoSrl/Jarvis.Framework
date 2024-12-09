using System.Threading.Tasks;

namespace Jarvis.Framework.Shared.ReadModel.Atomic
{
    /// <summary>
    /// <para>
    /// If an atomic reamodel needs to initialize something Ex Indexes, it should
    /// create an instance of this interface to perform initialization.
    /// </para>
    /// <para>It is not necessary for every readmodel to create such interface.</para>
    /// <para>
    /// It is duty of <see cref="AtomicProjectionEngine"/> to resolve and call
    /// setup for every registered readmodel.
    /// </para>
    /// </summary>
    public interface IAtomicReadModelInitializer
    {
        Task Initialize();
    }
}
