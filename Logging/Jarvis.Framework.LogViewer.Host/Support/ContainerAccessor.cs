using Castle.Windsor;

namespace Jarvis.Framework.LogViewer.Host.Support
{
    public static class ContainerAccessor
    {
        public static IWindsorContainer Instance { get; set; }
    }
}