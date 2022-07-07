using Castle.Core.Logging;
using Castle.Windsor;
using Jarvis.Framework.Shared.IdentitySupport;
using Jarvis.Framework.Shared.MultitenantSupport;

namespace Jarvis.Framework.Kernel.MultitenantSupport
{
    public class Tenant : ITenant
    {
        public ILogger Logger
        {
            get { return _logger; }
            set { _logger = value; }
        }

        private readonly TenantSettings _settings;
        private ILogger _logger = NullLogger.Instance;
        public IWindsorContainer Container { get; private set; }

        public Tenant(TenantSettings settings)
        {
            _settings = settings;
            _settings.Init();
            Container = new WindsorContainer();
        }

        public TenantId Id
        {
            get { return _settings.TenantId; }
        }

        public ICounterService CounterService
        {
            get { return _settings.CounterService; }
        }

        public T Get<T>(string key)
        {
            return _settings.Get<T>(key);
        }

        public string GetConnectionString(string name)
        {
            return _settings.GetConnectionString(name);
        }

        public bool HasBeedDisposed { get; private set; }

        public void Dispose()
        {
            if (Logger.IsDebugEnabled) Logger.DebugFormat("Disposing tenant {0}", _settings.TenantId);
            HasBeedDisposed = true;
        }
    }
}