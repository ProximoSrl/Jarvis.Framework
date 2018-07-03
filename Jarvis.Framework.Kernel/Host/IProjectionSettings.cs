using Castle.MicroKernel.Registration;
using Jarvis.Framework.Kernel.Support;
using Jarvis.Framework.Shared.IdentitySupport;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Jarvis.Framework.Kernel.Host
{
    /// <summary>
    /// This interface is used by single bounded context to 
    /// export all the interfaces that you want to run.
    /// </summary>
    public interface IProjectionSettings
    {
        /// <summary>
        /// Return a list of all the assemblies that contains projection engine.
        /// </summary>
        /// <returns></returns>
        IEnumerable<Type> GetProjectionAssemblies();

        /// <summary>
        /// It should register all the mongo convention for this bounded context 
        /// to use projection engine (identity, flat mapper and so on).
        /// </summary>
        void RegisterMongoConventions(IdentityManager identityManager);

        /// <summary>
        /// Return all shared assemblies
        /// </summary>
        /// <returns></returns>
        IEnumerable<Assembly> GetSharedAssemblies();

        /// <summary>
        /// give the ability to the caller to specify additional Windsor installers.
        /// </summary>
        /// <returns></returns>
        IEnumerable<IWindsorInstaller> GetAdditionalWindsorInstallers();
    }

    public abstract class ProjectionSettingsBase : IProjectionSettings
    {
        public IEnumerable<Type> GetProjectionAssemblies()
        {
            return OnGetProjectionAssemblies();
        }

        protected abstract IEnumerable<Type> OnGetProjectionAssemblies();

        public IEnumerable<Assembly> GetSharedAssemblies()
        {
            return OnGetSharedAssemblies();
        }

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
