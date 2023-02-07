using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Jarvis.Framework.Shared.ReadModel.Atomic
{
	/// <summary>
	/// Introduced with #44 this interface is an extension to <see cref="ILiveAtomicReadModelProcessor"/>
	/// that will allow to project more than one stream into one or more atomic readmodel. This is usually used
	/// to solve the situation where we need to project a series of stream up to a point in time with multiple
	/// readmodels.
	/// </summary>
	public interface ILiveAtomicMultistreamReadModelProcessor
	{
		/// <summary>
		/// Project multiple aggregate id, each one with potentially multiple <see cref="IAtomicReadModel"/>
		/// in a single call. Used to project a complete scenario in the past.
		/// </summary>
		/// <param name="request">List of aggregate to project.</param>
		/// <param name="checkpointUpToIncluded">Global checkpoint, included, to use as a limit for projection</param>
		/// <returns></returns>
		Task<MultiStreamProcessorResult> ProcessAsync(IReadOnlyCollection<MultiStreamProcessRequest> request, Int64 checkpointUpToIncluded);

		/// <summary>
		/// Project multiple aggregate id, each one with potentially multiple <see cref="IAtomicReadModel"/> in a single
		/// call up to a certain timestamp in the past.
		/// </summary>
		/// <param name="request">List of aggregate to project.</param>
		/// <param name="dateTimeUpTo">Project up to that date.</param>
		/// <returns></returns>
		Task<MultiStreamProcessorResult> ProcessAsync(IReadOnlyCollection<MultiStreamProcessRequest> request, DateTime dateTimeUpTo);
	}

	/// <summary>
	/// If we need to project more than one aggregate we need to specify a different 
	/// information for each aggregate. This structure contains information needed to 
	/// project a single aggregate.
	/// </summary>
	public struct MultiStreamProcessRequest
	{
		/// <summary>
		/// Default constructor.
		/// </summary>
		/// <param name="aggregateId"></param>
		/// <param name="readmodelTypes"></param>
		public MultiStreamProcessRequest(string aggregateId, IEnumerable<Type> readmodelTypes)
		{
			AggregateId = aggregateId;
			ReadmodelTypes = readmodelTypes;
		}

		/// <summary>
		/// Id of the aggregate we want to project
		/// </summary>
		public string AggregateId { get; private set; }

		/// <summary>
		/// Corresponding readmodel we want to project for aggregate with id <see cref="AggregateId"/>
		/// </summary>
		public IEnumerable<Type> ReadmodelTypes { get; private set; }
	}

	/// <summary>
	/// Contains result of a multi stream multi readmodel projection, this will project
	/// more than one PartionId in NStore where each stream can have more than one
	/// readmodel.
	/// </summary>
	public class MultiStreamProcessorResult
	{
		private Dictionary<string, IReadOnlyCollection<IAtomicReadModel>> _readmodels;

		internal MultiStreamProcessorResult(ICollection<IReadOnlyCollection<IAtomicReadModel>> readModels)
		{
			_readmodels = readModels
				.Where(r => r.Count > 0)
				.ToDictionary(r => r.First().Id);
		}

		/// <summary>
		/// Grab a readmodel, if no event is present no readmodel is created and the return
		/// value is null.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="id"></param>
		/// <returns></returns>
		/// <exception cref="NotImplementedException"></exception>
		public T Get<T>(string id) where T : IAtomicReadModel
		{
			if (!_readmodels.TryGetValue(id, out var readmodels))
			{
				return default;
			}

			var rm = (T)readmodels.FirstOrDefault(r => r.GetType() == typeof(T));
			return rm?.AggregateVersion > 0 ? rm : default;
		}
	}
}
