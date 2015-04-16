using Castle.MicroKernel;

namespace CQRS.Kernel.MultitenatSupport
{
    public interface ITenantServicesRegistration
    {
        
    }

    public class TenantServicesRegistration : ITenantServicesRegistration
    {
        private readonly IKernel _kernel;

        public TenantServicesRegistration(IKernel kernel)
        {
            _kernel = kernel;
        }
    }
}