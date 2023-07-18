using Jarvis.Framework.Shared.MultitenantSupport;
using System.Threading.Tasks;

namespace Jarvis.Framework.Shared.IdentitySupport
{
    public class MultitenantCounterService : ICounterService
    {
        private readonly ITenantAccessor _tenantAccessor;

        public MultitenantCounterService(ITenantAccessor tenantAccessor)
        {
            _tenantAccessor = tenantAccessor;
        }

        public Task ForceNextIdAsync(string serie, long nextIdToReturn)
        {
            return _tenantAccessor.Current.CounterService.ForceNextIdAsync(serie, nextIdToReturn);
        }

        public long GetNext(string serie)
        {
            return _tenantAccessor.Current.CounterService.GetNext(serie);
        }

        public Task<long> GetNextAsync(string serie)
        {
            return _tenantAccessor.Current.CounterService.GetNextAsync(serie);
        }

        public Task<long> PeekNextAsync(string serie)
        {
            return _tenantAccessor.Current.CounterService.PeekNextAsync(serie);
        }
    }
}