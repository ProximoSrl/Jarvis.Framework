using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using Castle.MicroKernel.Registration;
using Castle.MicroKernel.SubSystems.Configuration;
using Castle.Windsor;

namespace Jarvis.Framework.LogViewer.Host.Support
{
    public class ApiInstaller : IWindsorInstaller
    {
        public void Install(IWindsorContainer container, IConfigurationStore store)
        {
            container.Register(
                Classes
                    .FromThisAssembly()
                    .BasedOn<ApiController>()
                    .LifestyleTransient()
                );
        }
    }
}
