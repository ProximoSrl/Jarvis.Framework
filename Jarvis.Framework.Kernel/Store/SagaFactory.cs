using System;
using Castle.MicroKernel;
using Jarvis.NEventStoreEx.CommonDomainEx;
using Jarvis.NEventStoreEx.CommonDomainEx.Persistence;
using Fasterflect;
using Jarvis.Framework.Shared.Helpers;

namespace Jarvis.Framework.Kernel.Store
{
    public class SagaFactory : ISagaFactory
    {
        readonly IKernel _kernel;

        public SagaFactory(IKernel kernel = null)
        {
            _kernel = kernel;
        }

        public ISagaEx Build(Type sagaType, String id)
        {
            ISagaEx saga;
            if(_kernel != null && _kernel.HasComponent(sagaType))
                saga = (ISagaEx)_kernel.Resolve(sagaType);
            else
                saga = (ISagaEx)Activator.CreateInstance(sagaType);
            saga.SetPropertyValue(s => s.Id, id);
            return saga;
        }
    }
}