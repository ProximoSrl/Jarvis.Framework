using System;

namespace Jarvis.Framework.Shared.ReadModel
{
    public static class CollectionNames
    {
        public static Func<string, string> Customize = n => n;

        public static string GetCollectionName<TModel>() where TModel : IReadModel
        {
            var name = typeof(TModel).Name;
            if (name.EndsWith("ReadModel"))
                name = name.Remove(name.Length - "ReadModel".Length);
            return Customize(name);
        }
    }
}
