using System;
using System.Threading;
using Jarvis.Framework.Kernel.Events;
using Jarvis.Framework.Kernel.ProjectionEngine;
using Jarvis.Framework.Tests.EngineTests;
using Jarvis.Framework.Tests.SharedTests;
using Jarvis.Framework.Tests.SharedTests.IdentitySupport;

namespace Jarvis.Framework.Tests.ProjectionEngineTests
{
    [ProjectionInfo("Projection")]
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

    [ProjectionInfo("Projection2", Signature = "V2")]
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
            _collection.Insert(e, new SampleReadModel2()
            {
                Id = e.AggregateId,
                IsInRebuild = base.IsRebuilding,
                Timestamp = DateTime.Now.Ticks
            });
            Thread.Sleep(10);
        }
    }

    [ProjectionInfo("OtherSlotName", "v1", "Projection3")]
    public class Projection3 : AbstractProjection,
     IEventHandler<SampleAggregateCreated>
    {
        readonly ICollectionWrapper<SampleReadModel3, string> _collection;

        public Projection3(ICollectionWrapper<SampleReadModel3, string> collection)
        {
            _collection = collection;
            _collection.Attach(this, false);
            Signature = base.Info.Signature;
        }

        public override int Priority
        {
            get { return 3; } //higher priority than previous projection
        }

        public override void Drop()
        {
            _collection.Drop();
        }

        public String Signature {
            get { return _projectionInfoAttribute.Signature; }
            set { _projectionInfoAttribute = new ProjectionInfoAttribute(_projectionInfoAttribute.SlotName, value, _projectionInfoAttribute.CommonName); }
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

    [ProjectionInfo("ProjectionTypedId")]
    public class ProjectionTypedId : AbstractProjection
    {
        readonly ICollectionWrapper<SampleReadModelTest, TestId> _collection;

        public ProjectionTypedId(ICollectionWrapper<SampleReadModelTest, TestId> collection)
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
    }


    [ProjectionInfo("ProjectionPollableReadmodel")]
    public class ProjectionPollableReadmodel : AbstractProjection
    {
        readonly ICollectionWrapper<SampleReadModelPollableTest, TestId> _collection;

        public ProjectionPollableReadmodel(ICollectionWrapper<SampleReadModelPollableTest, TestId> collection)
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
    }
}