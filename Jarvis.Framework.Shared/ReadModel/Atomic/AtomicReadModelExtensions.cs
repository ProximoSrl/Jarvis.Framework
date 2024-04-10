using Castle.Core.Logging;
using Castle.MicroKernel.Registration;
using Castle.Windsor;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Jarvis.Framework.Shared.ReadModel.Atomic
{
    public static class AtomicReadModelExtensions
    {
        public static IEnumerable<Type> GetAllAtomicReadmodelFromCurrentAppDomain(ILogger logger)
        {
            List<Type> result = new List<Type>();
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    foreach (var type in assembly.GetTypes()
                        .Where(t => !t.IsAbstract && typeof(AbstractAtomicReadModel).IsAssignableFrom(t)))
                    {
                        result.Add(type);
                    }
                }
                catch (Exception ex)
                {
                    //We ignore exception, maybe some assembly cannot be loaded, it happens for SQL client for example.
                    logger.InfoFormat(ex, "GetAllAtomicReadmodelFromCurrentAppDomain: Error loading assembly {0}", assembly.FullName);
                }
            }
            return result.AsReadOnly();
        }

        public static void RegisterAllConventionsForAtomicReadmodelsInCurrentAppDomain(this IWindsorContainer container)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    container.Register(
                        Classes.FromAssembly(assembly)
                            .BasedOn<IAtomicReadModelInitializer>()
                            .WithServiceAllInterfaces());
                }
#pragma warning disable RCS1075 // Avoid empty catch clause that catches System.Exception.
                catch (Exception)
                {
                    //it is possible that some assembly cannot be registered for some reason, we can ignore the error
                }
#pragma warning restore RCS1075 // Avoid empty catch clause that catches System.Exception.
            }
        }
    }
}
