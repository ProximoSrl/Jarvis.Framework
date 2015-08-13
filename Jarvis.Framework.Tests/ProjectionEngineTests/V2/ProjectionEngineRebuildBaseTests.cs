using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Threading;
using Jarvis.Framework.Kernel.Events;
using Jarvis.Framework.Kernel.ProjectionEngine;
using Jarvis.Framework.Shared.IdentitySupport;
using Jarvis.Framework.Shared.Messages;
using Jarvis.Framework.Shared.ReadModel;
using Jarvis.Framework.TestHelpers;
using Jarvis.Framework.Tests.EngineTests;
using NUnit.Framework;

namespace Jarvis.Framework.Tests.ProjectionEngineTests.V2
{
    [TestFixture]
    public class ProjectionEngineRebuildBaseTests : AbstractV2ProjectionEngineTests
    {
        protected MongoReader<SampleReadModel, string> _reader1;
        protected MongoReader<SampleReadModel2, string> _reader2;
        protected MongoReader<SampleReadModel3, string> _reader3;

        [SetUp]
        public void TestSetup()
        {
            _reader1 = new MongoReader<SampleReadModel, string>(Database);
            _reader2 = new MongoReader<SampleReadModel2, string>(Database);
            _reader3 = new MongoReader<SampleReadModel3, string>(Database);
        }

        protected override void RegisterIdentities(IdentityManager identityConverter)
        {
            identityConverter.RegisterIdentitiesFromAssembly(typeof(SampleAggregateId).Assembly);
        }

        protected override string GetConnectionString()
        {
            return ConfigurationManager.ConnectionStrings["engine"].ConnectionString;
        }

        protected override IEnumerable<IProjection> BuildProjections()
        {
            var writer = new CollectionWrapper<SampleReadModel, string>(StorageFactory,new NotifyToNobody());
            yield return new Projection(writer);
        }

        private Boolean isRebuild = false;
        private Int32 initCount = 0;

        protected override bool OnShouldUseNitro()
        {
            return isRebuild;
        }

        protected override bool OnShouldRebuild()
        {
            initCount++;
            return isRebuild;
        }

        protected override void OnStartPolling()
        {
            Engine.Start();
        }

        protected void ReInitAndRebuild(int numberOfTotalCommits)
        {
            var collection = Database.GetCollection<SampleReadModel>("rm.Sample");
            collection.Drop(); //remove everything.
            isRebuild = true;
            ConfigureProjectionEngine(dropCheckpoints: false);
            Assert.That(initCount, Is.EqualTo(numberOfTotalCommits), "Reinit failed to setup rebuild.");

            DateTime startWait = DateTime.Now;
            while (_reader1.AllSortedById.Count() < numberOfTotalCommits &&
                   DateTime.Now.Subtract(startWait).TotalMilliseconds < 10000)
            {
                Thread.Sleep(50);
            }
        }
    }

    public class BasicRebuild : ProjectionEngineRebuildBaseTests
    {


        [Test]
        public async void start_then_rebuild()
        {
            
            var aggregate = TestAggregateFactory.Create<SampleAggregate, SampleAggregate.State>(new SampleAggregateId(1));
            aggregate.Create();
            Repository.Save(aggregate, Guid.NewGuid(), h => { });
            aggregate = TestAggregateFactory.Create<SampleAggregate, SampleAggregate.State>(new SampleAggregateId(2));
            aggregate.Create();
            Repository.Save(aggregate, Guid.NewGuid(), h => { });
            Thread.Sleep(50);
            await Engine.UpdateAndWait();
            Assert.AreEqual(2, _reader1.AllSortedById.Count());
            Assert.That(_reader1.AllSortedById.Count(r => r.IsInRebuild), Is.EqualTo(0));
        
            //now rebuild.
            ReInitAndRebuild(2);

            Assert.AreEqual(2, _reader1.AllSortedById.Count());
            Assert.That(_reader1.AllSortedById.Count(r => r.IsInRebuild), Is.EqualTo(2));
            var checkpoint = _checkpoints.FindOneById("Projection");
            Assert.That(checkpoint.Value, Is.EqualTo(2), "Checkpoint is written after rebuild.");
            Assert.That(checkpoint.Current, Is.EqualTo(2), "Checkpoint is written after rebuild.");
        }

    
    }


    public class BasicRebuildWithNewProjectionSameSlot : ProjectionEngineRebuildBaseTests
    {

        private Boolean returnTwoProjection = false;

        protected override IEnumerable<IProjection> BuildProjections()
        {
            var writer = new CollectionWrapper<SampleReadModel, string>(StorageFactory, new NotifyToNobody());
            yield return new Projection(writer);
            if (returnTwoProjection)
            {
                var writer2 = new CollectionWrapper<SampleReadModel2, string>(StorageFactory, new NotifyToNobody());
                yield return new Projection2(writer2);
            }

        }

        [Test]
        public async void verify_basic_rebuild_with_new_projection_same_slot()
        {

            var aggregate = TestAggregateFactory.Create<SampleAggregate, SampleAggregate.State>(new SampleAggregateId(1));
            aggregate.Create();
            Repository.Save(aggregate, Guid.NewGuid(), h => { });
            aggregate = TestAggregateFactory.Create<SampleAggregate, SampleAggregate.State>(new SampleAggregateId(2));
            aggregate.Create();
            Repository.Save(aggregate, Guid.NewGuid(), h => { });
            Thread.Sleep(50);
            await Engine.UpdateAndWait();
            Assert.AreEqual(2, _reader1.AllSortedById.Count());
            Assert.That(_reader1.AllSortedById.Count(r => r.IsInRebuild), Is.EqualTo(0));
            Assert.AreEqual(0, _reader2.AllSortedById.Count(), "Second projection should not be enabled!!");
           
            //now rebuild.
            returnTwoProjection = true;
            ReInitAndRebuild(2);

            Assert.AreEqual(2, _reader1.AllSortedById.Count());
            Assert.That(_reader1.AllSortedById.Count(r => r.IsInRebuild), Is.EqualTo(2));

            var checkpoint = _checkpoints.FindOneById("Projection");
            Assert.That(checkpoint.Value, Is.EqualTo(2), "Checkpoint is written after rebuild.");
            Assert.That(checkpoint.Current, Is.EqualTo(2), "Checkpoint is written after rebuild.");
        
            Assert.AreEqual(2, _reader2.AllSortedById.Count());
            Assert.That(_reader2.AllSortedById.Count(r => r.IsInRebuild), Is.EqualTo(2));
            checkpoint = _checkpoints.FindOneById("Projection2");
            Assert.That(checkpoint.Value, Is.EqualTo(2), "Checkpoint is written after rebuild.");
            Assert.That(checkpoint.Current, Is.EqualTo(2), "Checkpoint is written after rebuild.");
        
        }


    }
}
