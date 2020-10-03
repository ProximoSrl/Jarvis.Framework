using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jarvis.Framework.Shared.ReadModel
{
	/// <summary>
	/// A child readmodel should only have an id.
	/// </summary>
	public interface IAbstractChildReadModel
	{
		Object GetId();
	}

	/// <summary>
	/// Sometimes we need to have collection of sub read model in a 
	/// master read model. Those child read models should have an id
	/// to simplify management.
	/// </summary>
	/// <typeparam name="TKey">Type of the id key</typeparam>
	public abstract class AbstractChildReadModel<TKey> : IAbstractChildReadModel
	{
		public TKey Id { get; set; }

		public Object GetId()
		{
			return Id;
		}
	}

	public static class AbstractChildReadModelExtension
	{
		public static void AddOrReplace<T>(this List<T> list, T value) where T : IAbstractChildReadModel
		{
			list.RemoveAll(_ => _.GetId().Equals(value.GetId()));
			list.Add(value);
		}

		public static void AddOrReplace<T>(this IList<T> list, T value) where T : IAbstractChildReadModel
		{
			var itemToRemove = list.Where(_ => _.GetId().Equals(value.GetId()));
			foreach (var item in itemToRemove)
			{
				list.Remove(item);
			}
			list.Add(value);
		}
	}
}

