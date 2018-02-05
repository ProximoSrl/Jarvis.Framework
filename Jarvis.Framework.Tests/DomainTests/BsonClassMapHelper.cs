using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MongoDB.Bson.Serialization;

namespace Jarvis.Framework.Tests.DomainTests
{
    public static class BsonClassMapHelper
    {
        public static void Unregister<T>()
        {
            var classType = typeof(T);
            GetClassMap().Remove(classType);
        }

        private static Dictionary<Type, BsonClassMap> GetClassMap()
        {
            var cm = BsonClassMap.GetRegisteredClassMaps().FirstOrDefault();
            if (cm == null)
                return null;

            var fi = typeof(BsonClassMap).GetField("__classMaps", BindingFlags.Static | BindingFlags.NonPublic);
            var classMaps = (Dictionary<Type, BsonClassMap>)fi.GetValue(cm);
            return classMaps;
        }

        public static void Clear()
        {
            var cm = GetClassMap();
            if(cm != null)
			{
				cm.Clear();
			}
		}
    }
}