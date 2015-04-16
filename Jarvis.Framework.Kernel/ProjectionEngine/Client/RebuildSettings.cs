namespace Jarvis.Framework.Kernel.ProjectionEngine.Client
{
    public static class RebuildSettings
    {
        public static bool ShouldRebuild { get; private set; }
        public static bool NitroMode { get; private set; }

        public static void DisableRebuild()
        {
            ShouldRebuild = false;
            NitroMode = false;
        }

        public static void Init(bool shouldRebuild, bool nitroMode)
        {
            ShouldRebuild = shouldRebuild;
            NitroMode = shouldRebuild && nitroMode;
        }
    }
}
