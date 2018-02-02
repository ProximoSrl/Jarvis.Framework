using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Fasterflect;
using NStore.Domain;

namespace Jarvis.Framework.Shared.Helpers
{
    public static class RepositoryHelper
    {
        private static Dictionary<Type, GenericMethodInfo> _getByIdAsync1GenericCache = new Dictionary<Type, GenericMethodInfo>();
        private static Dictionary<Type, GenericMethodInfo> _saveAsync3GenericCache = new Dictionary<Type, GenericMethodInfo>();

        public static Task<IAggregate> GetByIdAsync(this IRepository repository, Type type, string id)
        {
            GenericMethodInfo genericMethodInfo;
            Type repositoryType = repository.GetType();
            if (!_getByIdAsync1GenericCache.TryGetValue(repositoryType, out genericMethodInfo))
            {
                genericMethodInfo = new GenericMethodInfo();
                genericMethodInfo.GenericMethodInfoDefinition = repositoryType
                    .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .Single(_ => _.Name == "GetByIdAsync"
                        && _.IsGenericMethod
                        && _.Parameters().Count == 1);
                _getByIdAsync1GenericCache[repositoryType] = genericMethodInfo;
            }

            MethodInfo minfo = genericMethodInfo.GetByIdAsync1(type);

            var task = ((Task)minfo.Call(repository, new Object[] { id }));

            return Task.FromResult((IAggregate)task.GetPropertyValue("Result"));
        }

        public static Task SaveAsync(this IRepository repository, Type type, Object aggregate, string operationId, Action<IHeadersAccessor> headers)
        {
            GenericMethodInfo genericMethodInfo;
            Type repositoryType = repository.GetType();
            if (!_saveAsync3GenericCache.TryGetValue(repositoryType, out genericMethodInfo))
            {
                genericMethodInfo = new GenericMethodInfo();
                genericMethodInfo.GenericMethodInfoDefinition = repositoryType
                    .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .Single(_ => _.Name == "SaveAsync"
                        && _.IsGenericMethod
                        && _.Parameters().Count == 3);
                _saveAsync3GenericCache[repositoryType] = genericMethodInfo;
            }

            MethodInfo minfo = genericMethodInfo.GetByIdAsync1(type);

            var task = ((Task)minfo.Call(repository, new Object[] { aggregate, operationId, headers }));
            task.Wait();
            return Task.CompletedTask;
        }

        private class GenericMethodInfo
        {
            public GenericMethodInfo()
            {
                _internalClosedTypeCache = new Dictionary<Type, MethodInfo>();
            }

            public MethodInfo GenericMethodInfoDefinition { get; set; }

            private readonly Dictionary<Type, MethodInfo> _internalClosedTypeCache;

            internal MethodInfo GetByIdAsync1(Type type)
            {
                MethodInfo minfo;
                if (!_internalClosedTypeCache.TryGetValue(type, out minfo))
                {
                    minfo = GenericMethodInfoDefinition.MakeGenericMethod(type);
                    _internalClosedTypeCache[type] = minfo;
                }
                return minfo;
            }
        }
    }
}
