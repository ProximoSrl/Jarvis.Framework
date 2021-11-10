using System;
using Castle.Core.Logging;
using Jarvis.Framework.Shared.Logging;
using NEventStore;

using NEventStore.Persistence.MongoDB;
using NEventStore.Serialization;

namespace Jarvis.Framework.Kernel.Engine
{
	public class EventStoreFactory
	{
	    private readonly ILoggerFactory _loggerFactory;

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
                new MongoPersistenceOptions() {};
            Wireup es = Wireup.Init()
                    //.LogTo(t => new NEventStoreLog4NetLogger(_loggerFactory.Create(t)))
                    .UsingMongoPersistence(
                        () => connectionString,
                        new DocumentObjectSerializer(),
                        mongoPersistenceOptions
                    )
                    .InitializeStorageEngine();

            UseSyncDispatcher = useSyncDispatcher;           

            if (hooks != null)
                es.HookIntoPipelineUsing(hooks);

            return es.Build();
        }
    }

   
}
