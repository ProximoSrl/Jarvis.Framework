using Jarvis.Framework.Shared.Exceptions;
using System;

namespace Jarvis.Framework.Shared.ReadModel
{
    public static class CollectionNames
    {
        public static Func<string, string> Customize = n => n;

        public static string GetCollectionName<TModel>() where TModel : IReadModel
        {
            return GetCollectionName(typeof(TModel));
        }

        public static string GetCollectionName(Type readmodelType)
        {
            if (!typeof(IReadModel).IsAssignableFrom(readmodelType))
            {
                throw new JarvisFrameworkEngineException($"Cannot extract collection name from type {readmodelType.FullName} because it does not implements IReadmodel");
            }

            var name = readmodelType.Name;
            if (name.EndsWith("ReadModel"))
            {
                name = name.Remove(name.Length - "ReadModel".Length);
            }

            return Customize(name);
        }
    }
}
