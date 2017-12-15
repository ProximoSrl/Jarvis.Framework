using Jarvis.Framework.Shared.Support;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using static Castle.MicroKernel.Registration.ServiceDescriptor;

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

			if (projectionInfoAttribute?.OfflineProjection == true && !OfflineMode.Enabled)
			{
				//avoid register this object with any interface, register as self
				return new Type[] { type };
			}

			return type.GetInterfaces();
		}
	}
}
