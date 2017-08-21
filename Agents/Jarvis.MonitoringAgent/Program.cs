using Castle.Core.Logging;
using Castle.Facilities.Logging;
using Castle.MicroKernel.Registration;
using Castle.MicroKernel.Resolvers.SpecializedResolvers;
using Castle.Windsor;
using Castle.Windsor.Configuration.Interpreters;
using Jarvis.MonitoringAgent.Support;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Topshelf;

namespace Jarvis.MonitoringAgent
{
    public class Program
    {
        static IWindsorContainer _container;
        static ILogger _logger;
        private static Bootstrapper _bootstrapper;

        static void Main(string[] args)
        {
            if (args.Length == 1 && (args[0] == "install" || args[0] == "uninstall"))
            {
                var runAsSystem = "true".Equals(
                   ConfigurationManager.AppSettings["runs-as-system"],
                       StringComparison.OrdinalIgnoreCase);
                var dependencies = ConfigurationManager.AppSettings["depends-on-services"] ?? "";

                StartForInstallOrUninstall();
            }
            else
            {
                StandardStart();
            }
            Console.WriteLine("Service is Stopped");
        }

        private static void StartForInstallOrUninstall()
        {
            HostFactory.Run(x =>
            {
                x.UseLog4Net("log4net.config");
                x.Service<Object>(s =>
                {
                    s.ConstructUsing(() => new Object());
                    s.WhenStarted(o => Console.WriteLine("Start for install"));
                    s.WhenStopped(o => Console.WriteLine("Stop for install"));
                });
                x.RunAsLocalSystem();
                x.DependsOnMsmq();

                x.SetDescription("Jarvis - Monitoring Agent");
                x.SetDisplayName("Jarvis - Monitoring Agent");
                x.SetServiceName("JarvisMonitoringAgent");
            });
        }



        private static void StandardStart()
        {
            if (Environment.UserInteractive)
            {
                Console.Title = "Jarvis - Monitoring Agent";
                Console.BackgroundColor = ConsoleColor.DarkRed;
                Console.Clear();
                Banner();
            }

            _container = new WindsorContainer();
            _container.Kernel.Resolver.AddSubResolver(new CollectionResolver(_container.Kernel, true));
            _container.Kernel.Resolver.AddSubResolver(new ArrayResolver(_container.Kernel, true));
            _container.AddFacility<LoggingFacility>(f =>
                f.LogUsing(LoggerImplementation.ExtendedLog4net)
                .WithConfig("log4net.config"));
            _logger = _container.Resolve<ILogger>();

            var configuration = new AppConfigMonitoringAgentConfiguration();
            _container.Register(
                Component
                    .For<MonitoringAgentConfiguration>()
                    .Instance(configuration)
            );

            _bootstrapper = new Bootstrapper(_container, _logger, configuration);

            HostFactory.Run(x =>
            {
                x.UseLog4Net("log4net.config");
                x.Service<Bootstrapper>(s =>
                {
                    s.ConstructUsing(name => _bootstrapper);
                    s.WhenStarted((tc, hc) => tc.Start(hc));
                    s.WhenStopped((tc, hc) => tc.Stop(hc));
                });
                x.RunAsLocalSystem();
                x.DependsOnMsmq();

                x.SetDescription("Jarvis - Monitoring Agent");
                x.SetDisplayName("Jarvis - Monitoring Agent");
                x.SetServiceName("JarvisMonitoringAgent");
            });
        }

        private static void Banner()
        {
            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine("===================================================================");
            Console.WriteLine("Jarvis - Monitoring Agent Service - Proximo srl");
            Console.WriteLine("===================================================================");
            Console.WriteLine("  install                            -> Installa il servizio");
            Console.WriteLine("  uninstall                          -> Rimuove il servizio");
            Console.WriteLine("  net start JarvisMonitoringAgent   -> Avvia il servizio");
            Console.WriteLine("  net stop JarvisMonitoringAgent    -> Arresta il servizio");
            Console.WriteLine("===================================================================");
            Console.WriteLine();
            Console.WriteLine();
        }
    }
}
