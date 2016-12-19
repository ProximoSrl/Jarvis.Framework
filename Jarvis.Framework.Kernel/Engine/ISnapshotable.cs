using Jarvis.Framework.Kernel.Store;
using Jarvis.NEventStoreEx.CommonDomainEx;
using System;

namespace Jarvis.Framework.Kernel.Engine
{
    public interface ISnapshotable
    {
		/// <summary>
		/// Restore snapshot into an object that support Snapshot.
		/// </summary>
		/// <param name="snapshot"></param>
		/// <returns>True if <paramref name="snapshot"/> object is compatible and valid
		/// with this aggregate, false otherwise. If the return is false it imply that
		/// the state of the object is also unchanged.</returns>
        Boolean Restore(IMementoEx snapshot);

		/// <summary>
		/// Get a snapshot for the object, the snapshot is an <see cref="IMementoEx"/> object that can
		/// be used to <see cref="ISnapshotable.Restore(IMementoEx)"/> method to restore the state of the
		/// object from the snapshot.
		/// </summary>
		/// <returns></returns>
        IMementoEx GetSnapshot();
    }

}