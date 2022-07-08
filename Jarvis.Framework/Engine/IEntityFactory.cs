using Castle.MicroKernel;
using Fasterflect;
using System;

namespace Jarvis.Framework.Kernel.Engine
{
    public interface IEntityFactory
    {
        T Create<T>() where T : IEntityRoot;
    }

    public class EntityFactoryEx
    {
        private readonly IKernel _kernel;

        public EntityFactoryEx(IKernel kernel)
        {
            _kernel = kernel;
        }

        public T Create<T>() where T : IEntityRoot
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

                return (T)ctor.CreateInstance();
            }
        }
    }
}
