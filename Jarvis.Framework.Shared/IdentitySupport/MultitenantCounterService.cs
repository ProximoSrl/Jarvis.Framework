using Jarvis.Framework.Shared.MultitenantSupport;
using System.Threading;
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

        public Task ForceNextIdAsync(string serie, long nextIdToReturn, CancellationToken cancellationToken = default)
        {
            return _tenantAccessor.Current.CounterService.ForceNextIdAsync(serie, nextIdToReturn, cancellationToken);
        }

        public long GetNext(string serie)
        {
            return _tenantAccessor.Current.CounterService.GetNext(serie);
        }

        public Task<long> GetNextAsync(string serie, CancellationToken cancellationToken = default)
        {
            return _tenantAccessor.Current.CounterService.GetNextAsync(serie, cancellationToken);
        }

        public Task<long> PeekNextAsync(string serie, CancellationToken cancellationToken = default)
        {
            return _tenantAccessor.Current.CounterService.PeekNextAsync(serie, cancellationToken);
        }
    }
}