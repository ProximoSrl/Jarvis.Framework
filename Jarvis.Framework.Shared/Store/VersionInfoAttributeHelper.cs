using System;
using System.Reflection;

namespace Jarvis.Framework.Shared.Store
{
    public static class VersionInfoAttributeHelper
    {
        internal static string GetEventName(Type type)
        {
            var eventInfo = type.GetCustomAttribute<VersionInfoAttribute>();
            if (eventInfo == null)
            {
                return $"{type.Name}_1";
            }

            var baseName = String.IsNullOrEmpty(eventInfo.Name) ? type.Name : eventInfo.Name;

            return $"{baseName}_{eventInfo.Version}";
        }
    }
}
