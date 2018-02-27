using Jarvis.Framework.Shared.Support;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Jarvis.Framework.Kernel.Events
{
    /// <summary>
    /// We need to really fix how projections are registered, this is a selector
    /// that should be used to register projections with all interfaces but check
    /// if the projection is in offline mode, because in offline mode a projection
    /// should not be registered.
    /// </summary>
    public static class ProjectionServiceSelector
	{
#pragma warning disable RCS1163 // Unused parameter.
		public static IEnumerable<Type> ServiceSelector(Type type, Type[] baseTypes)
#pragma warning restore RCS1163 // Unused parameter.
		{
			var projectionInfoAttribute = type.GetCustomAttribute<ProjectionInfoAttribute>();

            //This condition is used to avoid register in castle projections given some specifics
            //conditions. We have currently two condition why a projection should not run
            //1) is an offline projection and offline is not enabled
            //2) was disabled.
			if ((
                projectionInfoAttribute?.OfflineProjection == true && !OfflineMode.Enabled)
                || projectionInfoAttribute?.Disabled == true)    
			{
				//avoid register this object with any interface, register as self
				return new Type[] { type };
			}

			return type.GetInterfaces();
		}
	}
}
