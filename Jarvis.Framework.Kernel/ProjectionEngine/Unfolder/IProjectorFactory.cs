using Castle.MicroKernel;
using Fasterflect;
using Jarvis.Framework.Kernel.Engine;
using Jarvis.NEventStoreEx.CommonDomainEx;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Jarvis.Framework.Shared.Helpers;

namespace Jarvis.Framework.Kernel.ProjectionEngine.Unfolder
{
    /// <summary>
    /// Create a unfolder instance
    /// </summary>
    public interface IProjectorFactory
    {
        /// <summary>
        /// Create an unwinder.
        /// </summary>
        /// <typeparam name="TQueryModel"></typeparam>
        /// <param name="type"></param>
        /// <param name="id"></param>
        /// <returns></returns>
        Projector<TQueryModel> Create<TQueryModel>(Type type, IIdentity id) 
            where TQueryModel : BaseAggregateQueryModel, new();
    }
    
    /// <summary>
    /// Basic implementation of ProjectorFactory
    /// </summary>
    public class ProjectorFactory : IProjectorFactory
    {
        private readonly IKernel _kernel;

        public ProjectorFactory(IKernel kernel)
        {
            _kernel = kernel;
        }

        public Projector<TQueryModel> Create<TQueryModel>(Type type, IIdentity id)
            where TQueryModel : BaseAggregateQueryModel, new()
        {
            Projector<TQueryModel> projector = null;
            if (_kernel.HasComponent(type))
            {
                projector = (Projector<TQueryModel>)_kernel.Resolve(type);
            }
            else
            {
                var ctor = type.Constructor(Flags.Default, new Type[] { });

                if (ctor == null)
                    throw new MissingDefaultCtorException(type);

                projector = (Projector<TQueryModel>)ctor.CreateInstance();
            }
            projector.SetPropertyValue(d => d.Id, id);
            return projector;
        }

    }
}
