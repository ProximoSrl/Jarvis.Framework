using Castle.MicroKernel.Registration;
using Castle.Windsor;
using Jarvis.Framework.Kernel.Support;
using Jarvis.Framework.Shared.IdentitySupport;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Jarvis.Framework.Kernel.Host
{
    /// <summary>
    /// This interface is used by single bounded context to 
    /// export all the interfaces that you want to run offline.
    /// </summary>
    public interface IOfflineProjectionSettings
    {
        /// <summary>
        /// Return a list of all the projection for this bounded context that should
        /// run when jarvis is in offline mode.
        /// </summary>
        /// <returns></returns>
        IEnumerable<Type> GetOfflineProjections();

        /// <summary>
        /// It should register all the mongo convention for this bounded context 
        /// to use projection engine (identity, flat mapper and so on).
        /// </summary>
        void RegisterMongoConventions(IdentityManager identityManager);

        /// <summary>
        /// Give a list of all shared assemblies
        /// </summary>
        /// <returns></returns>
        IEnumerable<Assembly> GetSharedAssemblies();

        /// <summary>
        /// give the ability to the caller to specify additional Windsor installers.
        /// </summary>
        /// <returns></returns>
        IEnumerable<IWindsorInstaller> GetAdditionalWindsorInstallers();
    }

    [InheritedExport(typeof(IOfflineProjectionSettings))]
    public abstract class OfflineProjectionSettingsBase : IOfflineProjectionSettings
    {
        public IEnumerable<Type> GetOfflineProjections()
        {
            return OnGetOfflineProjection();
        }

        public IEnumerable<Assembly> GetSharedAssemblies()
        {
            return OnGetSharedAssemblies();
        }

        protected abstract IEnumerable<Type> OnGetOfflineProjection();

        public void RegisterMongoConventions(IdentityManager identityManager)
        {
            foreach (var assembly in OnGetSharedAssemblies())
            {
                MongoRegistration.RegisterAssembly(assembly);
                identityManager.RegisterIdentitiesFromAssembly(assembly);
            }
        }

        protected abstract IEnumerable<Assembly> OnGetSharedAssemblies();

        public virtual IEnumerable<IWindsorInstaller> GetAdditionalWindsorInstallers()
        {
            return new IWindsorInstaller[0];
        }
    }
}
