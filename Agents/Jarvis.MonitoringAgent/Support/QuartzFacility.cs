using Castle.Core.Logging;
using Castle.Facilities.QuartzIntegration;
using Castle.MicroKernel;
using Castle.MicroKernel.Facilities;
using Castle.MicroKernel.Registration;
using Quartz;
using Quartz.Spi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jarvis.MonitoringAgent.Support
{
    public class QuartzFacility : AbstractFacility
    {
        IDictionary<string, string> _configuration;
        private ILogger _logger;
        QuartzNetScheduler _scheduler;
        protected override void Init()
        {
            _logger = Kernel.Resolve<ILoggerFactory>().Create(GetType());

            AddComponent<IJobScheduler, QuartzNetSimpleScheduler>();
            AddComponent<IJobFactory, CastleJobFactory>();

            _scheduler = new QuartzNetScheduler(
                _configuration,
                Kernel.Resolve<IJobFactory>(),
                Kernel
            );
            Kernel.Register(Component.For<IScheduler>().Instance(_scheduler));
        }

        protected override void Dispose()
        {
            _scheduler.Dispose();
            base.Dispose();
        }
        internal string AddComponent<T>()
        {
            string key = typeof(T).AssemblyQualifiedName;
            Kernel.Register(Component.For(typeof(T)).Named(key));
            return key;
        }

        internal string AddComponent<I, T>() where T : I
        {
            string key = typeof(T).AssemblyQualifiedName;
            Kernel.Register(Component.For(typeof(I)).ImplementedBy(typeof(T)).Named(key));
            return key;
        }

        public void Configure(IDictionary<string, string> configuration)
        {
            _configuration = configuration;
        }
    }

    public class CastleJobFactory : IJobFactory
    {
        private readonly IKernel _kernel;

        public ILogger Logger { get; set; }

        /// <summary>
        /// Resolve a Job by it's name
        /// 
        /// </summary>
        public bool ResolveByJobName { get; set; }

        /// <summary>
        /// Creates a Quartz job with Windsor
        /// 
        /// </summary>
        /// <param name="kernel">Windsor Kernel</param>
        public CastleJobFactory(IKernel kernel)
        {
            this._kernel = kernel;
        }

        /// <summary>
        /// Called by the scheduler at the time of the trigger firing, in order to
        ///                         produce a <see cref="T:Quartz.IJob"/> instance on which to call Execute.
        /// 
        /// </summary>
        /// 
        /// <remarks>
        /// It should be extremely rare for this method to throw an exception -
        ///                         basically only the the case where there is no way at all to instantiate
        ///                         and prepare the Job for execution.  When the exception is thrown, the
        ///                         Scheduler will move all triggers associated with the Job into the
        ///                         <see cref="F:Quartz.TriggerState.Error"/> state, which will require human
        ///                         intervention (e.g. an application restart after fixing whatever
        ///                         configuration problem led to the issue wih instantiating the Job.
        /// 
        /// </remarks>
        /// <param name="bundle">The TriggerFiredBundle from which the <see cref="T:Quartz.IJobDetail"/>
        ///                           and other info relating to the trigger firing can be obtained.
        ///                         </param><param name="scheduler">a handle to the scheduler that is about to execute the job</param><throws>SchedulerException if there is a problem instantiating the Job. </throws>
        /// <returns>
        /// the newly instantiated Job
        /// 
        /// </returns>
        public IJob NewJob(TriggerFiredBundle bundle, IScheduler scheduler)
        {
            var jobType = bundle.JobDetail.JobType.FullName;
          
            var job = this.ResolveByJobName ?
                (IJob)_kernel.Resolve(bundle.JobDetail.Key.ToString(), typeof(IJob)) :
                (IJob)_kernel.Resolve(bundle.JobDetail.JobType);

            return job;
        }

        public void ReturnJob(IJob job)
        {
             _kernel.ReleaseComponent((object)job);
        }

       
    }
}
