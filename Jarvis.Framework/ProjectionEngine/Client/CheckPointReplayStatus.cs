namespace Jarvis.Framework.Kernel.ProjectionEngine.Client
{
    public sealed class CheckPointReplayStatus
    {
        public bool IsLast { get; private set; }
        public bool IsRebuilding { get; private set; }

        public CheckPointReplayStatus(bool isLast, bool isRebuilding)
        {
            IsLast = isLast;
            IsRebuilding = isRebuilding;
        }
    }
}