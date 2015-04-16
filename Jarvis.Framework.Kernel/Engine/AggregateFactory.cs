using System;
using Castle.MicroKernel;
using Fasterflect;
using Jarvis.Framework.Kernel.Store;
using Jarvis.Framework.Shared.IdentitySupport;
using Jarvis.NEventStoreEx.CommonDomainEx;
using Jarvis.NEventStoreEx.CommonDomainEx.Persistence.EventStore;

namespace Jarvis.Framework.Kernel.Engine
{
    public class MissingDefaultCtorException : Exception
    {
        public MissingDefaultCtorException(Type t)
            : base(string.Format("Type {0} has no default constructor. Add protected {1}(){{}}", t.FullName, t.Name))
        {
        }
    }

	public class AggregateFactory : IConstructAggregatesEx
    {
        private readonly IKernel _kernel;

        public AggregateFactory()
        {
            
        }

        public AggregateFactory(IKernel kernel)
        {
            _kernel = kernel;
        }

        public IAggregateEx Build(Type type, IIdentity id, IMementoEx snapshot)
        {
            AggregateRoot aggregate = null;
            if (_kernel != null && _kernel.HasComponent(type))
            {
                aggregate = (AggregateRoot)_kernel.Resolve(type);
            }
            else
            {
                var ctor = type.Constructor(Flags.Default, new Type[] { });

                if (ctor == null)
                    throw new MissingDefaultCtorException(type);

                aggregate = (AggregateRoot)ctor.CreateInstance();
            }

            aggregate.AssignAggregateId(id);

            if (snapshot != null && aggregate is ISnapshotable)
            {
                ((ISnapshotable)aggregate).Restore(snapshot);
            }

            return aggregate;
        }
    }
}
