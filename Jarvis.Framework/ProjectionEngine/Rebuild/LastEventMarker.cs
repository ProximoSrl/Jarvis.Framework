using Jarvis.Framework.Shared.Events;

namespace Jarvis.Framework.Kernel.ProjectionEngine.Rebuild
{
    /// <summary>
    /// <para>
    /// This is a fake events used during Rebuild to signal that we finished
    /// rebuilding everything. This will stop the rebuild for each slot
    /// and will force flush and closing of the rebuild.
    /// </para>
    /// <para>This events does not exists on the store, is just an InMemory marker</para>
    /// </summary>
    internal class LastEventMarker : DomainEvent
    {
        public readonly static LastEventMarker Instance = new LastEventMarker();

        private LastEventMarker()
        {
        }
    }
}
