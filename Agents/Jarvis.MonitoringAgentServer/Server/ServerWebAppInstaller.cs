using System;
using System.IO;
using System.Linq;
using System.Web.Http;
using Castle.Windsor;
using Jarvis.MonitoringAgentServer.Server;
using Jarvis.MonitoringAgentServer.Support;
using Microsoft.Owin;
using Microsoft.Owin.FileSystems;
using Microsoft.Owin.StaticFiles;
using Owin;

[assembly: OwinStartup(typeof(ServerWebAppInstaller))]
namespace Jarvis.MonitoringAgentServer.Server
{
    public class ServerWebAppInstaller
    {
        private static IWindsorContainer _container;

        public static void SetContainer(IWindsorContainer container)
        {
            _container = container;
        }

        public ServerWebAppInstaller()
        {

        }

        public void Configuration(IAppBuilder app)
        {
            ConfigureWebApi(app);
            ConfigureAdmin(app);
        }


        protected virtual void ConfigureWebApi(IAppBuilder appBuilder)
        {
            var config = new HttpConfiguration
            {
                DependencyResolver = new WindsorResolver(_container)
            };

            // Web API routes
            config.MapHttpAttributeRoutes();

            //config.Routes.MapHttpRoute(
            //    "DefaultServerApi",
            //    "api/{controller}/{action}/{id}",
            //    new { id = RouteParameter.Optional }
            //);

            appBuilder.UseWebApi(config);
        }

        private void ConfigureAdmin(IAppBuilder application)
        {
            var appFolder = FindAppRoot();

            var fileSystem = new PhysicalFileSystem(appFolder);

            var options = new FileServerOptions
            {
                EnableDirectoryBrowsing = true,
                FileSystem = fileSystem,
                EnableDefaultFiles = true
            };

            application.UseFileServer(options);
        }

        static string FindAppRoot()
        {
            var root = AppDomain.CurrentDomain.BaseDirectory
                .ToLowerInvariant()
                .Split(Path.DirectorySeparatorChar)
                .ToList();

            while (true)
            {
                var last = root.Last();
                if (last == String.Empty || last == "debug" || last == "release" || last == "bin")
                {
                    root.RemoveAt(root.Count - 1);
                    continue;
                }

                break;
            }

            root.Add("serverapp");

            var appFolder = String.Join("" + Path.DirectorySeparatorChar, root);
            return appFolder;
        }

    }
}
