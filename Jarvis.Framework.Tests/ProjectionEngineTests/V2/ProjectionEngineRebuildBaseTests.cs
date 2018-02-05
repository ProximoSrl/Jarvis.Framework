//using System;
//using System.Collections.Generic;
//using System.Configuration;
//using System.Linq;
//using System.Threading;
//using Jarvis.Framework.Kernel.Events;
//using Jarvis.Framework.Kernel.ProjectionEngine;
//using Jarvis.Framework.Shared.IdentitySupport;
//using Jarvis.Framework.Shared.Messages;
//using Jarvis.Framework.Shared.ReadModel;
//using Jarvis.Framework.TestHelpers;
//using Jarvis.Framework.Tests.EngineTests;
//using NUnit.Framework;
//using Jarvis.Framework.Kernel.ProjectionEngine.Client;
//using Jarvis.Framework.Shared.Helpers;
//using System.Threading.Tasks;

//namespace Jarvis.Framework.Tests.ProjectionEngineTests.V2
//{
//    public class ProjectionEngineRebuildBaseTests : AbstractV2ProjectionEngineTests
//    {
//        public ProjectionEngineRebuildBaseTests(String pollingClientVersion) : base(pollingClientVersion)
//        {

//        }

//        protected MongoReader<SampleReadModel, string> _reader1;
//        protected MongoReader<SampleReadModel2, string> _reader2;
//        protected MongoReader<SampleReadModel3, string> _reader3;

//        [SetUp]
//        public void TestSetup()
//        {
//            _reader1 = new MongoReader<SampleReadModel, string>(Database);
//            _reader2 = new MongoReader<SampleReadModel2, string>(Database);
//            _reader3 = new MongoReader<SampleReadModel3, string>(Database);
//        }

//        protected override void RegisterIdentities(IdentityManager identityConverter)
//        {
//            identityConverter.RegisterIdentitiesFromAssembly(typeof(SampleAggregateId).Assembly);
//        }

//        protected override string GetConnectionString()
//        {
//            return ConfigurationManager.ConnectionStrings["engine"].ConnectionString;
//        }

//        protected override IEnumerable<IProjection> BuildProjections()
//        {
//            var writer = new CollectionWrapper<SampleReadModel, string>(StorageFactory, new NotifyToNobody());
//            yield return new Projection(writer);
//        }

//        private Boolean isRebuild = false;
//        private Int32 initCount = 0;

//        protected override bool OnShouldUseNitro()
//        {
//            return isRebuild;
//        }

//        protected override bool OnShouldRebuild()
//        {
//            initCount++;
//            return isRebuild;
//        }

//        protected override Task OnStartPolling()
//        {
//            return Engine.StartAsync();
//        }

//        protected async Task ReInitAndRebuildAsync(int numberOfTotalCommits)
//        {
//            var collection = Database.GetCollection<SampleReadModel>("rm.Sample");
//            collection.Drop(); //remove everything.
//            isRebuild = true;
//            await ConfigureProjectionEngineAsync(dropCheckpoints: false).ConfigureAwait(false);
//            Assert.That(initCount, Is.EqualTo(numberOfTotalCommits), "Reinit failed to setup rebuild.");

//            DateTime startWait = DateTime.Now;
//            while (_reader1.AllSortedById.Count() < numberOfTotalCommits &&
//                   DateTime.Now.Subtract(startWait).TotalMilliseconds < 5000)
//            {
//                Thread.Sleep(50);
//            }

//            startWait = DateTime.Now;
//            Checkpoint checkpoint;
//            do
//            {
//                Thread.Sleep(50);
//                checkpoint = _checkpoints.FindOneById("Projection");
//            } while (
//                        (
//                            checkpoint == null ||
//                            checkpoint.Value != numberOfTotalCommits
//                         ) &&
//                         DateTime.Now.Subtract(startWait).TotalMilliseconds < 5000
//                     );
//        }
//    }

//    //[TestFixture("1")]
//    [TestFixture("2")]
//    public class BasicRebuild : ProjectionEngineRebuildBaseTests
//    {
//        public BasicRebuild(String commitPollingVersion) : base(commitPollingVersion)
//        {

//        }

//        [Test]
//        public async Task Start_then_rebuild()
//        {
//            var aggregate = await Repository.GetById < SampleAggregate>(new SampleAggregateId(1)).ConfigureAwait(false);
//            aggregate.Create();
//            await Repository.Save(aggregate, Guid.NewGuid().ToString(), h => { }).ConfigureAwait(false);
//            aggregate = await Repository.GetById<SampleAggregate>(new SampleAggregateId(2)).ConfigureAwait(false);
//            aggregate.Create();
//            await Repository.Save(aggregate, Guid.NewGuid().ToString(), h => { }).ConfigureAwait(false);
//            Thread.Sleep(50);
//            await Engine.UpdateAndWaitAsync().ConfigureAwait(false);
//            Assert.AreEqual(2, _reader1.AllSortedById.Count());
//            Assert.That(_reader1.AllSortedById.Count(r => r.IsInRebuild), Is.EqualTo(0));

//            //now rebuild.
//            await ReInitAndRebuildAsync(2).ConfigureAwait(false);

//            Assert.AreEqual(2, _reader1.AllSortedById.Count());
//            Assert.That(_reader1.AllSortedById.Count(r => r.IsInRebuild), Is.EqualTo(2));
//            var checkpoint = _checkpoints.FindOneById("Projection");
//            Assert.That(checkpoint.Value, Is.EqualTo(2), "Checkpoint Value is not written after rebuild.");
//            Assert.That(checkpoint.Current, Is.EqualTo(2), "Checkpoint Current is written after rebuild.");
//        }
//    }

//    ////[TestFixture("1")]
//    //[TestFixture("2")]
//    //public class BasicRebuildWithNewProjectionSameSlot : ProjectionEngineRebuildBaseTests
//    //{
//    //    public BasicRebuildWithNewProjectionSameSlot(String commitPollingVersion) : base(commitPollingVersion)
//    //    {

//    //    }

//    //    private Boolean returnTwoProjection = false;

//    //    protected override IEnumerable<IProjection> BuildProjections()
//    //    {
//    //        var writer = new CollectionWrapper<SampleReadModel, string>(StorageFactory, new NotifyToNobody());
//    //        yield return new Projection(writer);
//    //        if (returnTwoProjection)
//    //        {
//    //            var writer2 = new CollectionWrapper<SampleReadModel2, string>(StorageFactory, new NotifyToNobody());
//    //            yield return new Projection2(writer2);
//    //        }
//    //    }

//    //    [Test]
//    //    public async Task Verify_basic_rebuild_with_new_projection_same_slot()
//    //    {
//    //        var aggregate = await Repository.GetById < SampleAggregate>(new SampleAggregateId(1)).ConfigureAwait(false);
//    //        aggregate.Create();
//    //        await Repository.Save(aggregate,Guid.NewGuid().ToString(), h => { }).ConfigureAwait(false);
//    //        aggregate = await Repository.GetById < SampleAggregate>(new SampleAggregateId(2)).ConfigureAwait(false);
//    //        aggregate.Create();
//    //        await Repository.Save(aggregate,Guid.NewGuid().ToString(), h => { }).ConfigureAwait(false);
//    //        Thread.Sleep(50);
//    //        await Engine.UpdateAndWaitAsync().ConfigureAwait(false);
//    //        Assert.AreEqual(2, _reader1.AllSortedById.Count());
//    //        Assert.That(_reader1.AllSortedById.Count(r => r.IsInRebuild), Is.EqualTo(0));
//    //        Assert.AreEqual(0, _reader2.AllSortedById.Count(), "Second projection should not be enabled!!");

//    //        //now rebuild.
//    //        returnTwoProjection = true;
//    //        await ReInitAndRebuildAsync(2).ConfigureAwait(false);

//    //        Assert.AreEqual(2, _reader1.AllSortedById.Count());
//    //        Assert.That(_reader1.AllSortedById.Count(r => r.IsInRebuild), Is.EqualTo(2));

//    //        var checkpoint = _checkpoints.FindOneById("Projection");
//    //        Assert.That(checkpoint.Value, Is.EqualTo(2), "Checkpoint is written after rebuild.");
//    //        Assert.That(checkpoint.Current, Is.EqualTo(2), "Checkpoint is written after rebuild.");

//    //        Assert.AreEqual(2, _reader2.AllSortedById.Count());
//    //        Assert.That(_reader2.AllSortedById.Count(r => r.IsInRebuild), Is.EqualTo(2));
//    //        checkpoint = _checkpoints.FindOneById("Projection2");
//    //        Assert.That(checkpoint.Value, Is.EqualTo(2), "Checkpoint is written after rebuild.");
//    //        Assert.That(checkpoint.Current, Is.EqualTo(2), "Checkpoint is written after rebuild.");
//    //    }
//    //}

//    //[TestFixture("2")]
//    //public class BasicRebuildWithNewProjectionDifferentSlot : ProjectionEngineRebuildBaseTests
//    //{
//    //    public BasicRebuildWithNewProjectionDifferentSlot(String commitPollingVersion) : base(commitPollingVersion)
//    //    {

//    //    }

//    //    private Boolean returnTwoProjection = false;

//    //    protected override IEnumerable<IProjection> BuildProjections()
//    //    {
//    //        var writer = new CollectionWrapper<SampleReadModel, string>(StorageFactory, new NotifyToNobody());
//    //        yield return new Projection(writer);
//    //        if (returnTwoProjection)
//    //        {
//    //            var writer3 = new CollectionWrapper<SampleReadModel3, string>(StorageFactory, new NotifyToNobody());
//    //            yield return new Projection3(writer3);
//    //        }
//    //    }

//    //    //[Test]
//    //    //public async Task Verify_basic_rebuild_with_new_projection_different_slot()
//    //    //{
//    //    //    Assert.Inconclusive("We removed the ability to use the projection engine for rebuild, dedicated rebuildprojection engine is now used");
//    //    //    var aggregate = await Repository.GetById < SampleAggregate>(new SampleAggregateId(1)).ConfigureAwait(false);
//    //    //    aggregate.Create();
//    //    //    await Repository.Save(aggregate,Guid.NewGuid().ToString(), h => { }).ConfigureAwait(false);
//    //    //    aggregate = await Repository.GetById<SampleAggregate>(new SampleAggregateId(2)).ConfigureAwait(false);
//    //    //    aggregate.Create();
//    //    //    await Repository.Save(aggregate,Guid.NewGuid().ToString(), h => { }).ConfigureAwait(false);
//    //    //    Thread.Sleep(50);
//    //    //    await Engine.UpdateAndWaitAsync().ConfigureAwait(false);
//    //    //    Assert.AreEqual(2, _reader1.AllSortedById.Count());
//    //    //    Assert.That(_reader1.AllSortedById.Count(r => r.IsInRebuild), Is.EqualTo(0));
//    //    //    Assert.AreEqual(0, _reader3.AllSortedById.Count(), "Third projection should not be enabled!!");

//    //    //    //now rebuild.
//    //    //    returnTwoProjection = true;
//    //    //    await ReInitAndRebuildAsync(2).ConfigureAwait(false);

//    //    //    Assert.AreEqual(2, _reader1.AllSortedById.Count());
//    //    //    Assert.That(_reader1.AllSortedById.Count(r => r.IsInRebuild), Is.EqualTo(2));

//    //    //    var checkpoint = _checkpoints.FindOneById("Projection");
//    //    //    Assert.That(checkpoint.Value, Is.EqualTo(2), "Checkpoint is written after rebuild.");
//    //    //    Assert.That(checkpoint.Current, Is.EqualTo(2), "Checkpoint is written after rebuild.");

//    //    //    Assert.AreEqual(2, _reader3.AllSortedById.Count());
//    //    //    Assert.That(_reader3.AllSortedById.Count(r => r.IsInRebuild), Is.EqualTo(0), "New projection in new slot does not undergo rebuilding");
//    //    //    checkpoint = _checkpoints.FindOneById("Projection3");
//    //    //    Assert.That(checkpoint.Value, Is.EqualTo(2), "Checkpoint is written after rebuild.");
//    //    //    Assert.That(checkpoint.Current, Is.EqualTo(2), "Checkpoint is written after rebuild.");
//    //    //}
//    //}
//}
