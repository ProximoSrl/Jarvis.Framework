using Castle.MicroKernel.Registration;
using Jarvis.Framework.Kernel.Support;
using Jarvis.Framework.Shared.IdentitySupport;
using System.Collections.Generic;
using System.Reflection;

namespace Jarvis.Framework.Kernel.Host
{
    /// <summary>
    /// This interface is used by single bounded context to 
    /// export all the interfaces that you want to run.
    /// </summary>
    public interface IEngineSettings
    {
        /// <summary>
        /// Return a list of all the assemblies that contains command handlers
        /// to execute commands
        /// </summary>
        /// <returns></returns>
        IEnumerable<Assembly> GetCommandHandlerAssemblies();

        /// <summary>
        /// It should register all the mongo convention for this bounded context 
        /// to use projection engine (identity, flat mapper and so on).
        /// </summary>
        void RegisterMongoConventions(IdentityManager identityManager);

        /// <summary>
        /// Return a list of assembly that contains id to be reserved during
        /// id reservation phase.
        /// </summary>
        /// <returns></returns>
        IEnumerable<Assembly> GetAssembliesWithIdToReserve();

        /// <summary>
        /// Return list of shared assemblies.
        /// </summary>
        /// <returns></returns>
        IEnumerable<Assembly> GetSharedAssemblies();

        /// <summary>
        /// give the ability to the caller to specify additional Windsor installers.
        /// </summary>
        /// <returns></returns>
        IEnumerable<IWindsorInstaller> GetAdditionalWindsorInstallers();
    }

    public abstract class EngineSettingsBase : IEngineSettings
    {
        public IEnumerable<Assembly> GetCommandHandlerAssemblies()
        {
            return OnGetCommandHandlerAssemblies();
        }

        protected abstract IEnumerable<Assembly> OnGetCommandHandlerAssemblies();

        public IEnumerable<Assembly> GetSharedAssemblies()
        {
            return OnGetSharedAssemblies();
        }


        protected abstract IEnumerable<Assembly> OnGetSharedAssemblies();

        public void RegisterMongoConventions(IdentityManager identityManager)
        {
            foreach (var assembly in OnGetSharedAssemblies())
            {
                MongoRegistration.RegisterAssembly(assembly);
                identityManager.RegisterIdentitiesFromAssembly(assembly);
            }
        }

        public IEnumerable<Assembly> GetAssembliesWithIdToReserve()
        {
            return OnGetAssembliesWithIdToReserve();
        }

        protected abstract IEnumerable<Assembly> OnGetAssembliesWithIdToReserve();

        public virtual IEnumerable<IWindsorInstaller> GetAdditionalWindsorInstallers()
        {
            return new IWindsorInstaller[0];
        }
    }
}