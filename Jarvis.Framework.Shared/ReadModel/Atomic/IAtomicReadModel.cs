using NStore.Domain;
using System;
using System.Collections.Generic;

namespace Jarvis.Framework.Shared.ReadModel.Atomic
{
	public interface IAtomicReadModel : IReadModel
	{
		/// <summary>
		/// This is the id of the atomic readmodel.
		/// </summary>
		String Id { get; }

		/// <summary>
		/// <para>
		/// This is the position of last projection changeset.
		/// </para>
		/// <para>It is used to guarantee idempotency on write</para>
		/// </summary>
		Int64 ProjectedPosition { get; }

		/// <summary>
		/// Position of the specific stream that was projected.
		/// </summary>
		Int64 AggregateVersion { get; }

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
		/// Number of time that we tried to reproject the entire stream of events to
		/// fix the readmodel, it is needed to limit the number of time we can try to fix
		/// the readmodel.
		/// </summary>
		int FaultRetryCount { get; }

		/// <summary>
		/// This is a specific property that tells to the persistence wrapper that this
		/// specific readmodel cannot be persisted anymore because something happened like
		/// processing <see cref="Changeset"/> that comes from a different stream or from
		/// a Draft Stream.
		/// </summary>
		Boolean ModifiedWithExtraStreamEvents { get; }

        /// <summary>
        /// To avoid creating error in projection where we have version 2 and 3 and for some reason we
        /// dispatch version 3 before the 2, we keep a list of all version processed. This is done because
        /// we can admit holes, so if the aggregate is in version X we can tolerate processing X+2 version
        /// but if later X+1 is processed we have a problem and need to throw signaling that the aggregate
        /// is faulted due to a wrong ordering.
        /// </summary>
        List<long> LastProcessedVersions { get; }

        /// <summary>
        /// Mark the readmodel as faulted.
        /// </summary>
        /// <param name="projectedPosition">Position that caused failure</param>
        void MarkAsFaulted(Int64 projectedPosition);

		/// <summary>
		/// A readmodel atomic is capable of processing events.
		/// </summary>
		/// <param name="changeset"></param>
		Boolean ProcessChangeset(Changeset changeset);

		/// <summary>
		/// <para>
		/// To allow support for Draftable, we need to have a different
		/// method that will process the changeset that comes from a different
		/// persistence storage.
		/// </para>
		/// <para>
		/// This a concept where the readmodel will process a changeset that is not
		/// part of the stream, this should prevent the readmodel from being saved again
		/// to disk because it could generate confusion.
		/// </para>
		/// </summary>
		/// <param name="changeset"></param>
		void ProcessExtraStreamChangeset(Changeset changeset);
	}
}
