using Jarvis.Framework.Shared.Events;
using Jarvis.Framework.Shared.ReadModel;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace Jarvis.Framework.Kernel.ProjectionEngine
{
	public static class CollectionWrapperExtensions
	{
#pragma warning disable S2436 // Classes and methods should not have too many generic parameters
		public static async Task<List<TModel>> FindByPropertyAsListAsync<TModel, TKey, TValue>(
#pragma warning restore S2436 // Classes and methods should not have too many generic parameters
			this IMongoStorage<TModel, TKey> storage,
			Expression<Func<TModel, TValue>> propertySelector,
			TValue value) where TModel : class, IReadModelEx<TKey>
		{
			List<TModel> retValue = new List<TModel>();

			Func<TModel, Task> wrapper = m =>
			{
				retValue.Add(m);
				return Task.CompletedTask;
			};

			await storage.FindByPropertyAsync(propertySelector, value, wrapper).ConfigureAwait(false);

			return retValue;
		}

		public static Task<TModel> UpsertAsync<TModel, TKey>(
			this ICollectionWrapper<TModel, TKey> collectionWrapper,
			DomainEvent e,
			TKey id,
			Func<TModel> insert,
			Action<TModel> update,
			bool notify = false)
			where TModel : IReadModelEx<TKey>
		{
			Func<TModel, Task> wrapper = m =>
			{
				update(m);
				return Task.CompletedTask;
			};
			return collectionWrapper.UpsertAsync(e, id, insert, wrapper, notify);
		}

		public static Task FindAndModifyAsync<TModel, TKey>(
			this ICollectionWrapper<TModel, TKey> collectionWrapper,
			DomainEvent e,
			Expression<Func<TModel, bool>> filter,
			Action<TModel> action,
			bool notify = false)
			where TModel : IReadModelEx<TKey>
		{
			Func<TModel, Task> wrapper = m =>
			{
				action(m);
				return Task.CompletedTask;
			};
			return collectionWrapper.FindAndModifyAsync(e, filter, wrapper, notify);
		}

		public static Task FindAndModifyAsync<TModel, TKey>(
			this ICollectionWrapper<TModel, TKey> collectionWrapper,
			DomainEvent e,
			TKey id,
			Action<TModel> action,
			bool notify = false)
			where TModel : IReadModelEx<TKey>
		{
			Func<TModel, Task> wrapper = m =>
			{
				action(m);
				return Task.CompletedTask;
			};
			return collectionWrapper.FindAndModifyAsync(e, id, wrapper, notify);
		}

#pragma warning disable S2436 // Classes and methods should not have too many generic parameters
		public static Task FindByPropertyAsync<TModel, TKey, TProperty>(
#pragma warning restore S2436 // Classes and methods should not have too many generic parameters
			this ICollectionWrapper<TModel, TKey> collectionWrapper,
			Expression<Func<TModel, TProperty>> propertySelector,
			TProperty propertyValue,
			Action<TModel> subscription)
			where TModel : IReadModelEx<TKey>
		{
			Func<TModel, Task> wrapper = m =>
			{
				subscription(m);
				return Task.CompletedTask;
			};
			return collectionWrapper.FindByPropertyAsync<TProperty>(propertySelector, propertyValue, wrapper);
		}

#pragma warning disable S2436 // Classes and methods should not have too many generic parameters
		public static Task FindAndModifyByPropertyAsync<TModel, TKey, TProperty>(
#pragma warning restore S2436 // Classes and methods should not have too many generic parameters
			this ICollectionWrapper<TModel, TKey> collectionWrapper,
			DomainEvent e,
			Expression<Func<TModel, TProperty>> propertySelector,
			TProperty propertyValue,
			Action<TModel> action,
			bool notify = false)
			where TModel : IReadModelEx<TKey>
		{
			Func<TModel, Task> wrapper = m =>
			{
				action(m);
				return Task.CompletedTask;
			};

			return collectionWrapper.FindAndModifyByPropertyAsync<TProperty>(e, propertySelector, propertyValue, wrapper, notify);
		}
	}
}
