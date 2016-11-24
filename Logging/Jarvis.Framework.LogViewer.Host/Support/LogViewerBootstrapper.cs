using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http.Filters;
using Castle.Core.Logging;
using Castle.Facilities.Logging;
using Castle.MicroKernel.Registration;
using Castle.MicroKernel.Resolvers.SpecializedResolvers;
using Castle.Windsor;
using Microsoft.Owin.Hosting;
using MongoDB.Driver;
using MongoDB.Bson;

namespace Jarvis.Framework.LogViewer.Host.Support
{
    internal class LogViewerBootstrapper
    {
        IDisposable _webApplication;
        List<String> _serverAddressList;
        IWindsorContainer _container;
        ILogger _logger;

        public Boolean Start(LogViewerConfiguration config)
        {
            try
            {
                _serverAddressList = config
                    .ServerAddressList.Split(',')
                    .ToList();
                BuildContainer(config);
                ContainerAccessor.Instance = _container;
                if (_logger.IsDebugEnabled)  _logger.Debug("Log viewer starting");

                _container.Install(new ApiInstaller());
                var url = new MongoUrl(config.MongoDbConnection);
                var client = new MongoClient(url);
                var mongoDb = client.GetDatabase(url.DatabaseName);
                _container.Register(Component.For<IMongoDatabase>().Instance(mongoDb));
                _container.Register(
                    Component.For<IMongoCollection<BsonDocument>>().Instance(mongoDb.GetCollection<BsonDocument>(config.MongoDbDefaultCollectionLog)));

                StartOptions options = new StartOptions();
                foreach (var address in _serverAddressList)
                {
                    options.Urls.Add(address);
                }
                _webApplication = WebApp.Start<LogViewerApplication>(options);
                _logger.InfoFormat("Started server @ {0}", _serverAddressList.Aggregate((s1, s2) => s1 + ", " + s2));
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Exception: " + ex.ToString());
                Console.Error.WriteLine("Press a key to continue");
                Console.ReadKey();
                throw;
            }
           
            return true;
        }

        void BuildContainer(LogViewerConfiguration config)
        {
            _container = new WindsorContainer();
            _container.Register(Component.For<LogViewerConfiguration>().Instance(config));
            _container.Kernel.Resolver.AddSubResolver(new CollectionResolver(_container.Kernel, true));
            _container.Kernel.Resolver.AddSubResolver(new ArrayResolver(_container.Kernel, true));


            _container.AddFacility<LoggingFacility>(config.CreateLoggingFacility);

            _logger = _container.Resolve<ILoggerFactory>().Create(GetType());

        }

        private Boolean isStopped = false;
        public void Stop()
        {
            if (isStopped) return;

            //IMPORTANT: web application dispose WindsorContainer when disposed, so call 
            //to _webApplication.Dispose() should be done in the last call to stop.
            //IMPORTANT: disposing web application calls in DocumentBootstrapper for a second time.
            //need to check if the component was already stopped.
            isStopped = true;
            if (_webApplication != null)
            {
                _webApplication.Dispose();
            }
        }
    }
}
