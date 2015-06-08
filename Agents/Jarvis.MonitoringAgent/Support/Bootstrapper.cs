using System;
using System.Collections.Generic;
using Castle.Core.Logging;
using Castle.MicroKernel.Registration;
using Castle.Windsor;
using Jarvis.MonitoringAgent.Client.Jobs;
using Quartz;
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
                return StartMonitor();
            }
            catch (Exception ex)
            {
                _logger.ErrorFormat(ex, "Exception during startup: " + ex.Message);
                hostControl.Stop();
                return false;
            }
        }

        private IScheduler _scheduler;

        private bool StartMonitor()
        {
            
            _container.AddFacility<QuartzFacility>(c =>
                c.Configure(CreateDefaultConfiguration())
            );

            _scheduler = _container.Resolve<IScheduler>();

           
            //Register quartz jobs
            _container.Register(
                Classes.FromThisAssembly()
                    .BasedOn<IJob>()
                    .WithServiceSelf()
            );

            var job = JobBuilder.Create<LogPollerJob>()
                                .WithIdentity("LogPoller")
                                .Build();
            var trigger = TriggerBuilder.Create()
                .WithIdentity("trigger-LogPoller")
                .StartNow()
                .WithSimpleSchedule(builder => builder.WithIntervalInMinutes(1)
                .RepeatForever())
                .Build();
            _scheduler.ScheduleJob(job, trigger);

            job = JobBuilder.Create<LogUpdloaderJob>()
                   .WithIdentity("LogUploader")
                   .Build();

            trigger = TriggerBuilder.Create()
                .WithIdentity("trigger-LogUploader")
                .StartAt(DateTimeOffset.Now.AddSeconds(0))
                .WithSimpleSchedule(builder => builder.WithIntervalInMinutes(1)
                .RepeatForever())
                .Build();
            _scheduler.ScheduleJob(job, trigger);

            //_scheduler.Start();

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

        public Boolean Stop(HostControl hostControl)
        {
            _container.Dispose();
            return true;
        }
    }
}
