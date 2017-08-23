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
            if (_kernel?.HasComponent(typeof(T)) == true)
            {
                return _kernel.Resolve<T>();
            }
            else
            {
                var ctor = typeof(T).Constructor(Flags.Default, new Type[] { });

                if (ctor == null)
                    throw new MissingDefaultCtorException(typeof(T));

                return (T) ctor.CreateInstance();
            }
        }
    }
}
