using Jarvis.Framework.Shared.Exceptions;
using MongoDB.Bson;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Jarvis.Framework.Shared.IdentitySupport
{
	/// <summary>
	/// This counter service works offline and works thank to reservation.
	/// </summary>
	public class OfflineCounterService : IOfflineCounterService
	{
		readonly IMongoCollection<OfflineIdentity> _offlineSlots;

		public class OfflineIdentity
		{
			public OfflineIdentity(String serie, Int64 value)
			{
				Id = String.Format("{0}-{1}", serie, value);
				SerieName = serie;
				Value = value;
			}

			public string Id { get; private set; }

			public String SerieName { get; private set; }

			public Int64 Value { get; private set; }

			public Boolean Used { get; private set; }

			public DateTime UsedTimestamp { get; private set; }
		}

		public OfflineCounterService(IMongoDatabase db)
		{
			if (db == null) throw new ArgumentNullException("db");
			_offlineSlots = db.GetCollection<OfflineIdentity>("sysOfflineCounterSlots");
			_offlineSlots.Indexes.CreateOne(
                new CreateIndexModel<OfflineIdentity>(
				    Builders<OfflineIdentity>.IndexKeys
				    .Ascending(i => i.Used)
				    .Ascending(i => i.Value)
				    .Ascending(i => i.SerieName),
				    new CreateIndexOptions()
				    {
					    Name = "GetNextIndex"
				    })
                );
		}

		public long GetNext(string serie)
		{
			var counter = _offlineSlots.FindOneAndUpdate(
				Builders<OfflineIdentity>.Filter.And(
					Builders<OfflineIdentity>.Filter.Eq(x => x.SerieName, serie),
					Builders<OfflineIdentity>.Filter.Eq(x => x.Used, false)
				),
				Builders<OfflineIdentity>.Update
					.Set(x => x.Used, true)
					.Set(x => x.UsedTimestamp, DateTime.UtcNow),
				new FindOneAndUpdateOptions<OfflineIdentity, OfflineIdentity>()
				{
					ReturnDocument = ReturnDocument.After,
					IsUpsert = false,
					Sort = Builders<OfflineIdentity>.Sort.Ascending(x => x.Value)
				});
			if (counter == null)
			{
				throw new JarvisFrameworkEngineException("Unable to generate next number for serie " + serie);
			}
			return counter.Value;
		}

		public IEnumerable<string> GetReservationStarving(Int32 limit, params String[] series)
		{
			var aggregation = _offlineSlots.Aggregate()
			   .Match(Builders<OfflineIdentity>.Filter.Eq(x => x.Used, false))
			   .Group(BsonDocument.Parse("{_id: '$SerieName', 'count': { $sum : 1 } } "));
			//if we have series we want to know only element of that serie.
			if (series.Length > 0)
			{
				aggregation = aggregation.Match(Builders<BsonDocument>.Filter.In("_id", series));
			}

			var aggregationResult = aggregation
			   .ToEnumerable()
			   .ToDictionary(d => d["_id"].AsString, d => d["count"].AsInt32);

			//Remember that id that are not present are not returned from aggregation, they must be set to zero.
			if (series.Length > 0)
			{
				var missingKeys = series.Where(k => !aggregationResult.ContainsKey(k));
				foreach (var missingKey in missingKeys)
				{
					aggregationResult[missingKey] = 0;
				}
			}

			return aggregationResult
				.Where(d => d.Value < limit)
				.Select(d => d.Key);
		}

		public Int64 CountReservation(string serie)
		{
			return (Int64)_offlineSlots.Count(
				Builders<OfflineIdentity>.Filter.And(
					Builders<OfflineIdentity>.Filter.Eq(x => x.SerieName, serie),
					Builders<OfflineIdentity>.Filter.Eq(x => x.Used, false)
				));
		}

		public void AddReservation(string serie, ReservationSlot reservationSlot)
		{
			_offlineSlots.InsertMany(GetIdentityFromReservationSlot(serie, reservationSlot));
		}

		private IEnumerable<OfflineIdentity> GetIdentityFromReservationSlot(String serie, ReservationSlot reservationSlot)
		{
			for (Int64 i = reservationSlot.StartIndex; i <= reservationSlot.EndIndex; i++)
			{
				yield return new OfflineIdentity(serie, i);
			}
		}
	}
}
