using NStore.Domain;
using System;

namespace Jarvis.Framework.Shared.ReadModel.Atomic
{
    public interface IAtomicReadModel : IReadModel
    {
        /// <summary>
        /// This is the id of the atomic readmodel.
        /// </summary>
        String Id { get;  }

        /// <summary>
        /// <para>
        /// This is the position of last projection changeset.
        /// </para>
        /// <para>It is used to guarantee idempotency on write</para>
        /// </summary>
        Int64 ProjectedPosition { get; }

        /// <summary>
        /// Signature identify when a readmodel change, it should be changed every time
        /// that the readmodel changes.
        /// </summary>
        Int32 ReadModelVersion { get; }

        /// <summary>
        /// This property is set to true if processing some events raised error in the 
        /// readmodel.
        /// </summary>
        Boolean Faulted { get; }

        /// <summary>
        /// Mark the readmodel as faulted
        /// </summary>
        /// <returns></returns>
        void MarkAsFaulted(Int64 projectedPosition);

        /// <summary>
        /// A readmodel atomic is capable of processing events.
        /// </summary>
        /// <param name="changeset"></param>
        Boolean ProcessChangeset(Changeset changeset);
    }
}
