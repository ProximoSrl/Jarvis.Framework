using System;

namespace Jarvis.Framework.Shared.Store
{
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
	public class VersionInfoAttribute : Attribute
	{
		public string Name { get; set; }
		public int Version { get; set; }

		public VersionInfoAttribute()
		{
			Name = null;
			Version = 1;
		}
	}
}
