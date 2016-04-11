using System;
using System.Collections.Generic;
using System.Web.Http;
using Castle.Core.Logging;
using Castle.MicroKernel.Registration;
using Castle.Windsor;
using Jarvis.MonitoringAgentServer.Server;
using Jarvis.MonitoringAgentServer.Server.Data;
using Microsoft.Owin.Hosting;
using MongoDB.Driver;
using Quartz;
using Topshelf;

namespace Jarvis.MonitoringAgentServer.Support
{
    public class Bootstrapper
    {
        private IWindsorContainer _container;
        private ILogger _logger;
        private MonitoringAgentServerConfiguration _configuration;

        static IDisposable _app;

        public Bootstrapper(IWindsorContainer container, ILogger logger, MonitoringAgentServerConfiguration configuration)
        {
            _container = container;
            _logger = logger;
            _configuration = configuration;
        }

        public Boolean Start(HostControl hostControl)
        {
            try
            {
                return StartServer();
            }
            catch (Exception ex)
            {
                _logger.ErrorFormat(ex, "Exception during startup: " + ex.Message);
                hostControl.Stop();
                return false;
            }

            return true;
        }

    

        private bool StartServer()
        {
            _logger.Info("Starting Agent Server.");

            var url = new MongoUrl(_configuration.MongoConnectionString);
            var client = new MongoClient(url);
            var db = client.GetDatabase(url.DatabaseName);

            ServerWebAppInstaller.SetContainer(_container);
            var customerCollection = db.GetCollection<Customer>("serv.customers");
            _container.Register(
                Component
                    .For<IMongoCollection<Customer>>()
                    .Instance(customerCollection),
                  Component
                    .For<IMongoDatabase>()
                    .Instance(db),
                Classes.FromThisAssembly()
                    .BasedOn<ApiController>()
                    .LifestyleTransient());
            _app = WebApp.Start<ServerWebAppInstaller>(
                _configuration.ServerWebAppAddress);
            return true;
        }

        public Boolean Stop(HostControl hostControl)
        {
            _container.Dispose();
            return true;
        }
    }
}
