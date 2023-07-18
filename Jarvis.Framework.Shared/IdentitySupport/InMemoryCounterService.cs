using Jarvis.Framework.Shared.Exceptions;
using System.Threading.Tasks;

namespace Jarvis.Framework.Shared.IdentitySupport
{
    public class InMemoryCounterService : IReservableCounterService
    {
        long _last = 0;

        public Task ForceNextIdAsync(string serie, long nextIdToReturn)
        {
            if (_last >= nextIdToReturn)
            {
                throw new JarvisFrameworkEngineException($"Cannot force next id to a value that is less than current value: CurrentNext value is {_last} and you are trying to force it to {nextIdToReturn}");
            }
            _last = nextIdToReturn - 1;
            return Task.CompletedTask;
        }

        public long GetNext(string serie)
        {
            return ++_last;
        }

        public Task<long> GetNextAsync(string serie)
        {
            return Task.FromResult(++_last);
        }

        public Task<long> PeekNextAsync(string serie)
        {
            return Task.FromResult(_last+ 1);
        }

        public ReservationSlot Reserve(string serie, int amount)
        {
            var low = _last + 1;
            var high = low + amount;
            _last = high + 1;
            return new ReservationSlot(low, high);
        }
    }
}