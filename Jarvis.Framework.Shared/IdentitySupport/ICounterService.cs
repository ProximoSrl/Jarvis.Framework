using System;
using System.Collections.Generic;

namespace Jarvis.Framework.Shared.IdentitySupport
{
    public interface ICounterService
    {
        /// <summary>
        /// Get next counter for a given serie.
        /// </summary>
        /// <param name="serie"></param>
        /// <returns></returns>
        long GetNext(string serie);
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

    public interface IOfflineCounterService : ICounterService
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