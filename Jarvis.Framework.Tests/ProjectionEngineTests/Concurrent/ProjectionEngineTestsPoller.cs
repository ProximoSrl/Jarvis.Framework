﻿//using System;
//using System.Collections.Generic;
//using System.Configuration;
//using System.Linq;
//using Jarvis.Framework.Kernel.Events;
//using Jarvis.Framework.Kernel.ProjectionEngine;
//using Jarvis.Framework.Shared.IdentitySupport;
//using Jarvis.Framework.Shared.Messages;
//using Jarvis.Framework.Shared.ReadModel;
//using Jarvis.Framework.TestHelpers;
//using Jarvis.Framework.Tests.EngineTests;
//using NUnit.Framework;

//namespace Jarvis.Framework.Tests.ProjectionEngineTests.Concurrent
//{
//    [TestFixture]
//    public class ProjectionEngineTestsPoller : AbstractConcurrentProjectionEngineTests
//    {
//        [OneTimeSetUp]
//        public override void TestFixtureSetUp()
//        {
//            base.TestFixtureSetUp();
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

//        [Test]
//        public async void run_poller()
//        {
//            var reader = new MongoReader<SampleReadModel, string>(Database);
//            var aggregate = TestAggregateFactory.Create<SampleAggregate, SampleAggregate.State>(new SampleAggregateId(1));
//            aggregate.Create();
//            Repository.Save(aggregate,Guid.NewGuid().ToString(), h => { }).Wait();

//            await Engine.UpdateAndWait();
//            NUnit.Framework.Legacy.ClassicAssert.AreEqual(1, reader.AllSortedById.Count());

//            aggregate = TestAggregateFactory.Create<SampleAggregate, SampleAggregate.State>(new SampleAggregateId(2));
//            aggregate.Create();
//            Repository.Save(aggregate,Guid.NewGuid().ToString(), h => { }).Wait();

//            await Engine.UpdateAndWait();

//            NUnit.Framework.Legacy.ClassicAssert.AreEqual(2, reader.AllSortedById.Count());
//        }

//    }
//}