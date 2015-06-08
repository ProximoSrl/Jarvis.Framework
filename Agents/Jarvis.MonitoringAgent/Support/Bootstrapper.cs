using Castle.Core.Logging;
using Castle.MicroKernel.Registration;
using Castle.Windsor;
using Jarvis.MonitoringAgent.Client.Jobs;
using Jarvis.MonitoringAgent.Server;
using Jarvis.MonitoringAgent.Server.Data;
using Microsoft.Owin.Hosting;
using MongoDB.Driver;
using Quartz;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using Topshelf;

namespace Jarvis.MonitoringAgent.Support
{
    public class Bootstrapper
    {
        private IWindsorContainer _container;
        private ILogger _logger;
        private MonitoringAgentConfiguration _configuration;

        static IDisposable _app;

        public Bootstrapper(IWindsorContainer container, ILogger logger, MonitoringAgentConfiguration configuration)
        {
            _container = container;
            _logger = logger;
            _configuration = configuration;
        }

        public Boolean Start(HostControl hostControl)
        {
            try
            {
                switch (_configuration.Role)
                {
                    case Role.Server:
                        return StartServer();
                    case Role.Agent:
                        return StartMonitor();
                    default:
                        _logger.ErrorFormat("Unknown role {0}.", _configuration.Role);
                        return false;
                }

            }
            catch (Exception ex)
            {
                _logger.ErrorFormat(ex, "Exception during startup: " + ex.Message);
                hostControl.Stop();
                return false;
            }

            return true;
        }

        private bool StartMonitor()
        {
            _container.AddFacility<QuartzFacility>(c =>
                c.Configure(CreateDefaultConfiguration())
            );
            //Register quartz jobs
            _container.Register(
                Classes.FromThisAssembly()
                    .BasedOn<IJob>()
                    .WithServiceSelf()
            );

            var job = JobBuilder.Create<LogUploaderJob>()
                                .WithIdentity("LogUploader")
                                .Build();

            var trigger = TriggerBuilder.Create()
                .WithIdentity("trigger-LogUploader")
                .StartAt(DateTime.Now.AddSeconds(5))
                .WithSimpleSchedule(builder => builder.WithIntervalInMinutes(5)
                .RepeatForever())
                .Build();
            var scheduler =            _container.Resolve<IScheduler>();
            scheduler.ScheduleJob(job, trigger);
            scheduler.Start();

            return true;
        }

        private IDictionary<string, string> CreateDefaultConfiguration()
        {
            var config = new Dictionary<string, string>();
            config["quartz.scheduler.instanceId"] = Environment.MachineName + "-" + DateTime.Now.ToShortTimeString();
            config["quartz.threadPool.type"] = "Quartz.Simpl.SimpleThreadPool, Quartz";
            config["quartz.threadPool.threadCount"] = (Environment.ProcessorCount * 2).ToString();
            config["quartz.threadPool.threadPriority"] = "Normal";
            config["quartz.jobStore.type"] = "Quartz.Simpl.RAMJobStore, Quartz";
            return config;
        }

        private bool StartServer()
        {
            _logger.Info("Starting with server Role.");

            var url = new MongoUrl(_configuration.MongoConnectionString);
            var client = new MongoClient(url);
            var db = client.GetServer().GetDatabase(url.DatabaseName);

            ServerWebAppInstaller.SetContainer(_container);
            var customerCollection = db.GetCollection<Customer>("serv.customers");
            _container.Register(
                Component
                    .For<MongoCollection<Customer>>()
                    .Instance(customerCollection),
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
