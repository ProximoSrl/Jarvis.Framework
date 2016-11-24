using NEventStore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jarvis.NEventStoreEx.CommonDomainEx.Persistence
{
    /// <summary>
    /// This interface abstract the concept of a class that it is able to 
    /// persist and load <see cref="ISnapshot"/> instances.
    /// </summary>
    public interface ISnapshotPersister
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="snapshot"></param>
        /// <param name="type">We can have different type of snapshot, for standard aggregate or for 
        /// event unfolder and this parameter allows the caller to specify a type that generates the snapshot</param>
        void Persist(ISnapshot snapshot, String type);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="streamId"></param>
        /// <param name="versionUpTo">If you need to restore an aggregate at version X you 
        /// should get a snapshot that is not greater than X. This parameter allow you to avoid 
        /// loading a snapshot taken at version token greater than this value.</param>
        /// <param name="type">To allow saving different type of snapshot for a single
        /// StreamId the type is used to distinguish aggregate type.</param>
        /// <returns></returns>
        ISnapshot Load(String streamId, Int32 versionUpTo, String type);

        /// <summary>
        /// Clear all checkpoints taken before a certain checkpoint. 
        /// </summary>
        /// <param name="streamId"></param>
        /// <param name="versionUpTo"></param>
        /// <param name="type">To allow saving different type of snapshot for a single
        /// StreamId the type is used to distinguish aggregate type.</param>
        void Clear(String streamId, Int32 versionUpTo, String type);
    }
}
