using Castle.MicroKernel;
using Fasterflect;
using NStore.Domain;
using System;

namespace Jarvis.Framework.Kernel.Engine
{
    public class AggregateFactoryEx : IAggregateFactory
    {
        private readonly IKernel _kernel;

        public AggregateFactoryEx(IKernel kernel)
        {
            _kernel = kernel;
        }

        public T Create<T>() where T : IAggregate
        {
            return (T)Create(typeof(T));
        }

        public IAggregate Create(Type aggregateType)
        {
            if (_kernel?.HasComponent(aggregateType) == true)
            {
                return (IAggregate)_kernel.Resolve(aggregateType);
            }
            else
            {
                var ctor = aggregateType.Constructor(Flags.Default, new Type[] { });

                if (ctor == null)
                    throw new MissingDefaultCtorException(aggregateType);

                return (IAggregate) ctor.CreateInstance();
            }
        }
    }
}
