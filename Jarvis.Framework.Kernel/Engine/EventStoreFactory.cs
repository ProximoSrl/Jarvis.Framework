using System;

namespace Jarvis.Framework.Kernel.Engine
{
    public static class EventStoreFactory
    {
        /// <summary>
        /// Default partition collection name for NStore, TODO it should be made parameteric.
        /// </summary>
        public const String PartitionCollectionName = "Commits";
    }
}
