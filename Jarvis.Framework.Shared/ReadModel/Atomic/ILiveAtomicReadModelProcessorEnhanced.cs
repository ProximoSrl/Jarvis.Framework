using MongoDB.Driver.Linq;
using NStore.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Jarvis.Framework.Shared.ReadModel.Atomic
{
	/// <summary>
	/// Processor that is capable of live projection of atomic
	/// readmodels, it will allow for historical projection when
	/// you have a readmodel and you want to project up to a certain
	/// point in time.
	/// </summary>
	public interface ILiveAtomicReadModelProcessorEnhanced : ILiveAtomicReadModelProcessor
    {
        /// <summary>
        /// Project a single aggregate into multiple readmodels, useful when we need
        /// to project an aggregate that has multiple readmodels. #45
        /// </summary>
        /// <param name="types"></param>
        /// <param name="id">If of the aggregate</param>
        /// <param name="versionUpTo">Version of aggregate to project, it is NOT the global
        /// position, but version of aggregate.
        /// </param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<MultipleReadmodelProjectionResult> ProcessAsync(IEnumerable<Type> types, String id, Int64 versionUpTo, CancellationToken cancellationToken = default);

        /// <summary>
        /// For temporal reamodel we can be interested in projecting a readmodel
        /// until a certain position relative to the entire stream.
        /// </summary>
        /// <param name="types"></param>
        /// <param name="id">If of the aggregate</param>
        /// <param name="positionUpTo">Global position in the stream to use for projection.</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<MultipleReadmodelProjectionResult> ProcessUntilChunkPositionAsync(IEnumerable<Type> types, String id, Int64 positionUpTo, CancellationToken cancellationToken = default);

        /// <summary>
        /// For temporal reamodel we can be interested in projecting a readmodel up to a certain
        /// date in time. We know that date can be tricky, but it is a convenient method to be
        /// used because we do not have timestamp as first level citizen in NStore.
        /// </summary>
        /// <param name="types"></param>
        /// <param name="id">If of the aggregate</param>
        /// <param name="dateTimeUpTo">Date to project up to, you must specify UTC date time value not local time</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<MultipleReadmodelProjectionResult> ProcessUntilUtcTimestampAsync<TModel>(IEnumerable<Type> types, String id, DateTime dateTimeUpTo, CancellationToken cancellationToken = default);

        /// <summary>
        /// Process a readmodel but let the caller to specify a condition to stop the processing. The <paramref name="readModelConditionEvaluator"/> must
        /// return true if we want to stop the processing. Returned value is the readmodel after it processed the <see cref="Changeset"/> that
        /// made it transition to the state that made the condition true.
        ///
        /// We can stop also if the changeset has some condition expressed by <paramref name="changesetConditionEvaluator"/>. In this situation if the
        /// condition evaluate to true the changeset that cause it to return true IS NOT APPLIED to the readmodel.
        /// 
        /// If only one of the
        /// conditions return true the processing will stop.
        /// </summary>
        /// <typeparam name="TModel"></typeparam>
        /// <param name="id">Id of the readmodel</param>
        /// <param name="readModel">The caller can specify a starting readmodel that will be used to project. Usually this parameter is null
        /// if you want to project a new readmodel.</param>
        /// <param name="changesetConditionEvaluator"></param>
        /// <param name="readModelConditionEvaluator"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<TModel> ProcessUntilAsync<TModel>(
            string id,
            TModel readModel,
            Func<Changeset, TModel, CancellationToken, Task<Boolean>> changesetConditionEvaluator,
            Func<TModel, CancellationToken, Task<Boolean>> readModelConditionEvaluator,
            CancellationToken cancellationToken = default) where TModel : IAtomicReadModel;
    }

	/// <summary>
	/// Sample helper class to simplify accessing projection of multiple readmodels.
	/// </summary>
	public class MultipleReadmodelProjectionResult : Dictionary<Type, IAtomicReadModel>
	{
		/// <summary>
		/// Create from a readmodel list.
		/// </summary>
		/// <param name="readmodels"></param>
		public MultipleReadmodelProjectionResult(IEnumerable<IAtomicReadModel> readmodels)
		{
			foreach (var rm in readmodels.Where(r => r.AggregateVersion > 0))
			{
				this[rm.GetType()] = rm;
			}
		}

		/// <summary>
		/// Get specific readmodel
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <returns></returns>
		public T Get<T>()
		{
			if (TryGetValue(typeof(T), out var readmodel))
			{
				return (T)readmodel;
			}

			return default;
		}
	}
}
