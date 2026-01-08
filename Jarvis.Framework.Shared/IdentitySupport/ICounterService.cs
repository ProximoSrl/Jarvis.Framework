using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Jarvis.Framework.Shared.IdentitySupport
{
    public interface ICounterService
    {
        /// <summary>
        /// Get next counter for a given serie and update underlying storage.
        /// </summary>
        /// <param name="serie"></param>
        /// <returns></returns>
        long GetNext(string serie);

        /// <summary>
        /// Async version of <see cref="GetNext(string)"/>
        /// </summary>
        /// <param name="serie"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<long> GetNextAsync(string serie, CancellationToken cancellationToken = default);

        /// <summary>
        /// Force next id to be the one specified, will throw if the id is already used.
        /// </summary>
        /// <param name="nextIdToReturn">Specify the next id that will be returned by the counter
        /// service. If the value is less or equal of already generated id this will throw.</param>
        /// ù <param name="serie">The serie to force</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task ForceNextIdAsync(string serie, long nextIdToReturn, CancellationToken cancellationToken = default);

        /// <summary>
        /// Return the next id that will be generated for the specified identity but without incrementing
        /// internal counter. It is only used to understand what is the next id that will be generated.
        /// </summary>
        /// <param name="serie">Serie to query</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<long> PeekNextAsync(string serie, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Counter service that can setup a starting value for each sequence.
    /// </summary>
    public interface ICounterServiceWithOffset : ICounterService
    {
        /// <summary>
        /// Ensure that the next value is not less than given value. It is sometimes
        /// necessary when you need a sequence that does not collide with some other
        /// already existing value (ex. when you import data from other systems.)
        /// This is somewhat obsolete after the introduction of <see cref="ForceNextIdAsync(string, long)"/>
        /// </summary>
        /// <param name="serie"></param>
        /// <param name="minValue"></param>
        void EnsureMinimumValue(String serie, Int64 minValue);
    }

    public interface IReservableCounterService : ICounterService
    {
        /// <summary>
        /// Reserve a certain amount of ids for offline generation. This will
        /// change the actual counter to never generate identity for the reservation
        /// slot.  <br />
        /// The reservation slot is usually consumed by an
        /// </summary>
        /// <param name="serie">The serie we want to reserve a slot into. </param>
        /// <param name="amount">The amount of id to reserve</param>
        /// <returns></returns>
        ReservationSlot Reserve(string serie, Int32 amount);
    }

    public interface IOfflineCounterService : IReservableCounterService
    {
        /// <summary>
        /// An offline counter service can generate only counters that
        /// are reserved. This method allows to add a reservation to a current
        /// <see cref="IOfflineCounterService"/> instance
        /// </summary>
        /// <param name="serie">The serie we want to add the reservation to.</param>
        /// <param name="reservationSlot">A reservation slot returned from a standard ICounterService</param>
        void AddReservation(string serie, ReservationSlot reservationSlot);

        /// <summary>
        /// Return how much ids are still available for a given serie.
        /// </summary>
        /// <param name="serie"></param>
        /// <returns></returns>
        Int64 CountReservation(string serie);

        /// <summary>
        /// Return all series that have less than <paramref name="limit"/> number
        /// of id available for generation.
        /// </summary>
        /// <param name="limit"></param>
        /// <param name="series">If you specify some serie name, only these series
        /// are checked for starvation, but also if the serie has no reservation it
        /// returns.</param>
        /// <returns></returns>
        /// <remarks>If <paramref name="series"/> is empty, this methods returns only the series that actually
        /// have some reservation into the system, while if you specify at least one <paramref name="series"/>
        /// all series that have zero reservation are retuned.</remarks>
        IEnumerable<string> GetReservationStarving(Int32 limit, params String[] series);
    }
}