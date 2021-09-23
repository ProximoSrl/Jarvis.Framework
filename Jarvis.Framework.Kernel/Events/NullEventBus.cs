using Castle.Core.Logging;
using Jarvis.Framework.Shared.Events;
using Newtonsoft.Json;

namespace Jarvis.Framework.Kernel.Events
{
    public sealed class NullEventBus : IEventBus
    {
        private ILogger _logger = NullLogger.Instance;

        public ILogger Logger
        {
            get { return _logger; }
            set { _logger = value; }
        }

        public void Publish(DomainEvent e)
        {
            if (Logger.IsDebugEnabled)
            {
                Logger.DebugFormat("Event on bus -> {0}\n{1}", e.GetType().FullName, JsonConvert.SerializeObject(e, Formatting.Indented));
            }
        }
    }
}
