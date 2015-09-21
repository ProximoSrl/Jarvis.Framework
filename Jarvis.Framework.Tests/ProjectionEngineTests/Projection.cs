using System;
using System.Threading;
using Jarvis.Framework.Kernel.Events;
using Jarvis.Framework.Kernel.ProjectionEngine;
using Jarvis.Framework.Tests.EngineTests;

namespace Jarvis.Framework.Tests.ProjectionEngineTests
{
    public class Projection : AbstractProjection,
        IEventHandler<SampleAggregateCreated>
    {
        readonly ICollectionWrapper<SampleReadModel, string> _collection;

        public Projection(ICollectionWrapper<SampleReadModel, string> collection)
        {
            _collection = collection;
            _collection.Attach(this, false);
        }

        public override void Drop()
        {
            _collection.Drop();
        }

        public override void SetUp()
        {
        }

        public void On(SampleAggregateCreated e)
        {
            _collection.Insert(e, new SampleReadModel()
            {
                Id = e.AggregateId,
                IsInRebuild = base.IsRebuilding,
                Timestamp = DateTime.Now.Ticks
            });
        }
    }


    public class Projection2 : AbstractProjection,
       IEventHandler<SampleAggregateCreated>
    {
        readonly ICollectionWrapper<SampleReadModel2, string> _collection;

        public Projection2(ICollectionWrapper<SampleReadModel2, string> collection)
        {
            _collection = collection;
            _collection.Attach(this, false);
        }

        public override int Priority
        {
            get { return 3; } //higher priority than previous projection
        }

        public override void Drop()
        {
            _collection.Drop();
        }

        public override void SetUp()
        {
        }

        public void On(SampleAggregateCreated e)
        {
            Thread.Sleep(0);
            _collection.Insert(e, new SampleReadModel2()
            {
                Id = e.AggregateId,
                IsInRebuild = base.IsRebuilding,
                Timestamp = DateTime.Now.Ticks
            });
        }
    }

    public class Projection3 : AbstractProjection,
     IEventHandler<SampleAggregateCreated>
    {
        readonly ICollectionWrapper<SampleReadModel3, string> _collection;

        public Projection3(ICollectionWrapper<SampleReadModel3, string> collection)
        {
            _collection = collection;
            _collection.Attach(this, false);
        }

        public override int Priority
        {
            get { return 3; } //higher priority than previous projection
        }

        public override void Drop()
        {
            _collection.Drop();
        }
        public override string GetSlotName()
        {
            return "OtherSlotName";
        }

        public override void SetUp()
        {
        }

        public void On(SampleAggregateCreated e)
        {
            Console.WriteLine("Projected in thread {0} - {1}", 
                Thread.CurrentThread.ManagedThreadId,
                Thread.CurrentThread.Name);
            Thread.Sleep(0);
            _collection.Insert(e, new SampleReadModel3()
            {
                Id = e.AggregateId,
                IsInRebuild = base.IsRebuilding,
                Timestamp = DateTime.Now.Ticks
            });
        }
    }
}