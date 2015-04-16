using System;
using Castle.Core.Logging;
using Jarvis.Framework.Shared.Logging;
using NEventStore;
using NEventStore.Dispatcher;
using NEventStore.Persistence.MongoDB;
using NEventStore.Serialization;

namespace Jarvis.Framework.Kernel.Engine
{
	public class EventStoreFactory
	{
	    private readonly ILoggerFactory _loggerFactory;
	    public IDispatchCommits CommitsDispatcher { get; set; }
	    public bool UseSyncDispatcher { get; set; }

        public EventStoreFactory(ILoggerFactory loggerFactory)
	    {
	        _loggerFactory = loggerFactory;
	    }

        public IStoreEvents BuildEventStore(
            string connectionString, 
            IPipelineHook[] hooks = null, 
            Boolean useSyncDispatcher = false,
            MongoPersistenceOptions mongoPersistenceOptions = null)
        {
            mongoPersistenceOptions = mongoPersistenceOptions ??
                new MongoPersistenceOptions() { ServerSideOptimisticLoop = true };
            Wireup es = Wireup.Init()
                    .LogTo(t => new NEventStoreLog4NetLogger(_loggerFactory.Create(t)))
                    .UsingMongoPersistence(
                        () => connectionString,
                        new DocumentObjectSerializer(),
                        mongoPersistenceOptions
                    )
                    .InitializeStorageEngine();

            UseSyncDispatcher = useSyncDispatcher;

            if (CommitsDispatcher != null &&  !(CommitsDispatcher is NullDispatcher))
            {
                if (UseSyncDispatcher)
                    es = es.UsingSynchronousDispatchScheduler().DispatchTo(CommitsDispatcher);
                else
                    es = es.UsingAsynchronousDispatchScheduler().DispatchTo(CommitsDispatcher);
            }
            else
            {
                es.DoNotDispatchCommits();
            }

            if (hooks != null)
                es.HookIntoPipelineUsing(hooks);

            return es.Build();
        }
    }

    public static class NullDispatchSchedulerWireupExtensions
    {
        public static NullDispatchSchedulerWireup DoNotDispatchCommits(this Wireup wireup)
        {
            return new NullDispatchSchedulerWireup(wireup);
        }
    }

    public class NullDispatchSchedulerWireup : Wireup
    {
        public NullDispatchSchedulerWireup(Wireup wireup)
            : base(wireup)
        {
            Container.Register<IScheduleDispatches>(c=> new NullDispatchScheduler());
        }
    }

    public  class NullDispatchScheduler : IScheduleDispatches
    {
        public void Dispose()
        {
        }

        public void ScheduleDispatch(ICommit commit)
        {
        }

        public void Start()
        {
            
        }
    }
}
