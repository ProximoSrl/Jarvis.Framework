namespace Jarvis.Framework.Kernel.ProjectionEngine.Client
{
	public static class RebuildSettings
	{
		public static bool ShouldRebuild { get; private set; }
		public static bool NitroMode { get; private set; }

		/// <summary>
		/// If this value is true, we are doing standard events dispatching
		/// but the <see cref="CheckPointReplayStatus.IsRebuilding"/> value
		/// is always true as if we are in rebuild and <see cref="CheckPointReplayStatus.IsLast"/>
		/// is always false. <br />
		/// Used to do parallel rebuild or for offline sync.
		/// </summary>
		public static bool ContinuousRebuild { get; private set; }

		public static void DisableRebuild()
		{
			ShouldRebuild = false;
			ContinuousRebuild = false;
			NitroMode = false;
		}

		public static void Init(
			bool shouldRebuild,
			bool nitroMode,
			bool continuousRebuild = false)
		{
			ShouldRebuild = shouldRebuild;
			NitroMode = shouldRebuild && nitroMode;
			ContinuousRebuild = continuousRebuild;
		}
	}
}
