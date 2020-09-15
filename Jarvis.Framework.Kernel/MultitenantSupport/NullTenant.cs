using Castle.Windsor;
using Jarvis.Framework.Kernel.MultitenantSupport.Exceptions;
using Jarvis.Framework.Shared.IdentitySupport;
using Jarvis.Framework.Shared.MultitenantSupport;

namespace Jarvis.Framework.Kernel.MultitenantSupport
{
    public class NullTenant : ITenant
    {
        public static readonly ITenant Instance = new NullTenant();

        public ICounterService CounterService
        {
            get { throw new InvalidTenantException(); }
        }

        public T Get<T>(string key)
        {
            throw new System.NotImplementedException();
        }

        public bool HasBeedDisposed
        {
            get { throw new InvalidTenantException(); }
        }

        public TenantId Id { get; private set; }
        public IWindsorContainer Container
        {
            get
            {
                throw new System.NotImplementedException();
            }
        }

        public string GetConnectionString(string name)
        {
            throw new System.NotImplementedException();
        }

        public void Dispose()
        {
        }
    }
}