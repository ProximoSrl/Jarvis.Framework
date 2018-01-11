using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jarvis.Framework.Shared.Support
{
	/// <summary>
	/// TODO: Refactor, this is a quick way to setup a global settings
	/// that can be used to diagnose if the code is running in offline mode.
	/// </summary>
	public static class OfflineMode
	{
		public static Boolean Enabled { get; private set; }
		public static Object OfflineContext { get; private set; }

		/// <summary>
		/// Set offline 
		/// </summary>
		/// <param name="offlineContext">A context object that the user can 
		/// store to got information about offline mode.</param>
		public static void SetOfflineMode(Object offlineContext)
		{
			Enabled = true;
			OfflineContext = offlineContext;
		}

		public static void ResetOfflineMode()
		{
			Enabled = false;
			OfflineContext = null;
		}
	}
}
