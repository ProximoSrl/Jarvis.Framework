using Jarvis.Framework.Kernel.ProjectionEngine.Atomic;
using Jarvis.Framework.Shared.Events;
using Jarvis.Framework.Shared.Helpers;
using Jarvis.Framework.Shared.ReadModel.Atomic;
using Jarvis.Framework.Tests.ProjectionsTests.Atomic.Support;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Threading.Tasks;

namespace Jarvis.Framework.Tests.ProjectionsTests.Atomic
{
    [TestFixture]
    public class LiveAtomicReadModelProcessorTests : AtomicProjectionEngineTestBase
    {
        #region Classic single aggregate

        [Test]
        public async Task Project_distinct_entities()
        {
            var c1 = await GenerateSomeChangesetsAndReturnLatestsChangeset().ConfigureAwait(false);

            _aggregateIdSeed++;
            var c2 = await GenerateSomeChangesetsAndReturnLatestsChangeset().ConfigureAwait(false);
            await GenerateTouchedEvent().ConfigureAwait(false);

            //ok we need to check that events are not mixed.
            var sut = _container.Resolve<ILiveAtomicReadModelProcessor>();
            var processed = await sut.ProcessAsync<SimpleTestAtomicReadModel>(((DomainEvent)c1.Events[0]).AggregateId, Int32.MaxValue).ConfigureAwait(false);

            Assert.That(processed.TouchCount, Is.EqualTo(2));

            processed = await sut.ProcessAsync<SimpleTestAtomicReadModel>(((DomainEvent)c2.Events[0]).AggregateId, Int32.MaxValue).ConfigureAwait(false);

            Assert.That(processed.TouchCount, Is.EqualTo(3));
        }

        [Test]
        public async Task Project_up_until_certain_aggregate_version()
        {
            var c1 = await GenerateSomeChangesetsAndReturnLatestsChangeset().ConfigureAwait(false);

            var c2 = await GenerateTouchedEvent().ConfigureAwait(false);

            //ok we need to check that events are not mixed.
            var sut = _container.Resolve<ILiveAtomicReadModelProcessor>();
            var processed = await sut.ProcessAsync<SimpleTestAtomicReadModel>(((DomainEvent)c1.Events[0]).AggregateId, c1.AggregateVersion).ConfigureAwait(false);

            Assert.That(processed.TouchCount, Is.EqualTo(2));

            processed = await sut.ProcessAsync<SimpleTestAtomicReadModel>(((DomainEvent)c1.Events[0]).AggregateId, c2.AggregateVersion).ConfigureAwait(false);

            Assert.That(processed.TouchCount, Is.EqualTo(3));
        }

        [Test]
        public async Task Project_non_existing_aggregate()
        {
            var sut = _container.Resolve<ILiveAtomicReadModelProcessor>();
            var processed = await sut.ProcessAsyncUntilChunkPosition<SimpleTestAtomicReadModel>(
                  "SampleAggregate_123123123",
                  0L).ConfigureAwait(false);

            Assert.That(processed, Is.Null, "Aggregate does not exists at that checkpoint, we expect null");

            processed = await sut.ProcessAsync<SimpleTestAtomicReadModel>(
               "SampleAggregate_123123123",
               0L).ConfigureAwait(false);

            Assert.That(processed, Is.Null, "Aggregate does not exists at that checkpoint, we expect null");

            processed = await sut.ProcessAsyncUntilUtcTimestamp<SimpleTestAtomicReadModel>(
              "SampleAggregate_123123123",
              DateTime.Now).ConfigureAwait(false);

            Assert.That(processed, Is.Null, "Aggregate does not exists at that checkpoint, we expect null");
        }

        [Test]
        public async Task Project_before_the_object_exists()
        {
            var c1 = await GenerateSomeChangesetsAndReturnLatestsChangeset().ConfigureAwait(false);
            var c2 = await GenerateTouchedEvent().ConfigureAwait(false);

            //ok we need to check that events are not mixed.
            var sut = _container.Resolve<ILiveAtomicReadModelProcessor>();
            DomainEvent firstEvent = (DomainEvent)c1.Events[0];
            var processed = await sut.ProcessAsyncUntilChunkPosition<SimpleTestAtomicReadModel>(
                firstEvent.AggregateId.AsString(),
                firstEvent.CheckpointToken).ConfigureAwait(false);

            Assert.That(processed.TouchCount, Is.EqualTo(2));
            Assert.That(processed.AggregateVersion, Is.EqualTo(3));

            firstEvent = (DomainEvent)c2.Events[0];
            processed = await sut.ProcessAsyncUntilChunkPosition<SimpleTestAtomicReadModel>(
                  firstEvent.AggregateId.AsString(),
                  0L).ConfigureAwait(false);

            Assert.That(processed, Is.Null, "Aggregate does not exists at that checkpoint, we expect null");

            processed = await sut.ProcessAsync<SimpleTestAtomicReadModel>(
               firstEvent.AggregateId.AsString(),
               0L).ConfigureAwait(false);

            Assert.That(processed, Is.Null, "Aggregate does not exists at that checkpoint, we expect null");

            processed = await sut.ProcessAsyncUntilUtcTimestamp<SimpleTestAtomicReadModel>(
              firstEvent.AggregateId.AsString(),
              DateTime.MinValue).ConfigureAwait(false);

            Assert.That(processed, Is.Null, "Aggregate does not exists at that checkpoint, we expect null");
        }

        [Test]
        public async Task Project_up_until_certain_checkpoint_number()
        {
            var c1 = await GenerateSomeChangesetsAndReturnLatestsChangeset().ConfigureAwait(false);
            var c2 = await GenerateTouchedEvent().ConfigureAwait(false);

            //ok we need to check that events are not mixed.
            var sut = _container.Resolve<ILiveAtomicReadModelProcessor>();
            DomainEvent firstEvent = (DomainEvent)c1.Events[0];
            var processed = await sut.ProcessAsyncUntilChunkPosition<SimpleTestAtomicReadModel>(
                firstEvent.AggregateId.AsString(),
                firstEvent.CheckpointToken).ConfigureAwait(false);

            Assert.That(processed.TouchCount, Is.EqualTo(2));
            Assert.That(processed.AggregateVersion, Is.EqualTo(3));

            firstEvent = (DomainEvent)c2.Events[0];
            processed = await sut.ProcessAsyncUntilChunkPosition<SimpleTestAtomicReadModel>(
                  firstEvent.AggregateId.AsString(),
                  firstEvent.CheckpointToken).ConfigureAwait(false);

            Assert.That(processed.TouchCount, Is.EqualTo(3));
            Assert.That(processed.AggregateVersion, Is.EqualTo(4));
        }

        [Test]
        public async Task Project_up_until_certain_date()
        {
            var c1 = await GenerateSomeChangesetsAndReturnLatestsChangeset().ConfigureAwait(false);

            //First block generates 3 events and two touch this is another touch 3 touch and version 4 (creation event)
            await GenerateTouchedEvent().ConfigureAwait(false);

            //we are at version 4 now put events in the future.

            DateTime future1 = DateTime.UtcNow.AddMinutes(1);
            await GenerateTouchedEvent(timestamp: future1);

            DateTime future2 = DateTime.UtcNow.AddMinutes(3);
            await GenerateTouchedEvent(timestamp: future2);

            //ok we need to check that events are not mixed.
            var sut = _container.Resolve<ILiveAtomicReadModelProcessor>();
            DomainEvent firstEvent = (DomainEvent)c1.Events[0];

            //Process until now, we should have everythign up to latest two events.
            var processed = await sut.ProcessAsyncUntilUtcTimestamp<SimpleTestAtomicReadModel>(
                firstEvent.AggregateId.AsString(),
                DateTime.UtcNow).ConfigureAwait(false);

            Assert.That(processed.TouchCount, Is.EqualTo(3));
            Assert.That(processed.AggregateVersion, Is.EqualTo(4));

            //project up to future 1
            processed = await sut.ProcessAsyncUntilUtcTimestamp<SimpleTestAtomicReadModel>(
                  firstEvent.AggregateId.AsString(),
                  future1).ConfigureAwait(false);

            Assert.That(processed.TouchCount, Is.EqualTo(4));
            Assert.That(processed.AggregateVersion, Is.EqualTo(5));

            //project up to future 2
            processed = await sut.ProcessAsyncUntilUtcTimestamp<SimpleTestAtomicReadModel>(
                  firstEvent.AggregateId.AsString(),
                  future2).ConfigureAwait(false);

            Assert.That(processed.TouchCount, Is.EqualTo(5));
            Assert.That(processed.AggregateVersion, Is.EqualTo(6));

            //project up to the max
            processed = await sut.ProcessAsyncUntilUtcTimestamp<SimpleTestAtomicReadModel>(
                  firstEvent.AggregateId.AsString(),
                  DateTime.UtcNow.AddYears(2000)).ConfigureAwait(false);

            Assert.That(processed.TouchCount, Is.EqualTo(5));
            Assert.That(processed.AggregateVersion, Is.EqualTo(6));
        }

        [Test]
        public async Task Capability_of_catchup_events()
        {
            await GenerateSomeChangesetsAndReturnLatestsChangeset().ConfigureAwait(false);
            var c2 = await GenerateSomeChangesetsAndReturnLatestsChangeset().ConfigureAwait(false);
            var c3 = await GenerateTouchedEvent().ConfigureAwait(false);

            //Arrange: manually process some events in readmodel
            var firstEvent = (DomainEvent)c2.Events[0];
            var sut = _container.Resolve<ILiveAtomicReadModelProcessor>();
            var rm = await sut.ProcessAsync<SimpleTestAtomicReadModel>(
                firstEvent.AggregateId.AsString(),
                c2.AggregateVersion).ConfigureAwait(false);

            Assert.That(rm.AggregateVersion, Is.EqualTo(c2.AggregateVersion));
            var touchCount = rm.TouchCount;

            //Act, ask to catchup events.       
            await sut.CatchupAsync(rm).ConfigureAwait(false);

            Assert.That(rm.AggregateVersion, Is.EqualTo(c3.AggregateVersion));
            Assert.That(rm.TouchCount, Is.EqualTo(touchCount + 1));
        }

        [Test]
        public async Task Cacthup_events_with_no_more_Events_works()
        {
            await GenerateSomeChangesetsAndReturnLatestsChangeset().ConfigureAwait(false);
            var c2 = await GenerateSomeChangesetsAndReturnLatestsChangeset().ConfigureAwait(false);

            //Arrange: manually process some events in readmodel
            var firstEvent = (DomainEvent)c2.Events[0];
            var sut = _container.Resolve<ILiveAtomicReadModelProcessor>();
            var rm = await sut.ProcessAsync<SimpleTestAtomicReadModel>(
                firstEvent.AggregateId.AsString(),
                c2.AggregateVersion).ConfigureAwait(false);

            Assert.That(rm.AggregateVersion, Is.EqualTo(c2.AggregateVersion));
            var touchCount = rm.TouchCount;

            //Act, ask to catchup events.       
            await sut.CatchupAsync(rm).ConfigureAwait(false);

            Assert.That(rm.AggregateVersion, Is.EqualTo(c2.AggregateVersion));
            Assert.That(rm.TouchCount, Is.EqualTo(touchCount));
        }

        [Test]
        public async Task Capability_of_catchup_to_project_everything()
        {
            await GenerateSomeChangesetsAndReturnLatestsChangeset().ConfigureAwait(false);
            await GenerateSomeChangesetsAndReturnLatestsChangeset().ConfigureAwait(false);
            var c3 = await GenerateTouchedEvent().ConfigureAwait(false);

            //Arrange: manually process some events in readmodel
            var firstEvent = (DomainEvent)c3.Events[0];
            var sut = _container.Resolve<ILiveAtomicReadModelProcessor>();
            var rm = new SimpleTestAtomicReadModel(firstEvent.AggregateId);

            //Act, ask to catchup events.       
            await sut.CatchupAsync(rm).ConfigureAwait(false);

            Assert.That(rm.AggregateVersion, Is.EqualTo(c3.AggregateVersion));
            Assert.That(rm.TouchCount, Is.EqualTo(3)); //we have 3 touch events.
        }

        #endregion

        #region Specific tests

        [Test]
        public async Task Project_more_than_one_readmodel()
        {
            var c1 = await GenerateSomeChangesetsAndReturnLatestsChangeset().ConfigureAwait(false);
            await GenerateTouchedEvent().ConfigureAwait(false);

            //ok we need to check that events are not mixed.
            var sut = _container.Resolve<ILiveAtomicReadModelProcessorEnhanced>();
            var aggregateId = ((DomainEvent)c1.Events[0]).AggregateId;
            var processResult = await sut.ProcessAsync(
                new Type[] { typeof(SimpleTestAtomicReadModel), typeof(SimpleAtomicReadmodelWithSingleEventHandled) },
                aggregateId,
                Int32.MaxValue).ConfigureAwait(false);

            var simple = processResult[typeof(SimpleTestAtomicReadModel)] as SimpleTestAtomicReadModel;
            Assert.That(simple.TouchCount, Is.EqualTo(3));
            Assert.That(simple.Created, Is.True);
            Assert.That(simple.AggregateVersion, Is.EqualTo(4));

            var seh = processResult[typeof(SimpleAtomicReadmodelWithSingleEventHandled)] as SimpleAtomicReadmodelWithSingleEventHandled;
            Assert.That(seh.TouchCount, Is.EqualTo(3));
            Assert.That(seh.AggregateVersion, Is.EqualTo(4));

            //Now project up to version 1
            processResult = await sut.ProcessAsync(
                new Type[] { typeof(SimpleTestAtomicReadModel), typeof(SimpleAtomicReadmodelWithSingleEventHandled) },
                aggregateId,
                1).ConfigureAwait(false);

            simple = processResult[typeof(SimpleTestAtomicReadModel)] as SimpleTestAtomicReadModel;
            Assert.That(simple.TouchCount, Is.EqualTo(0));
            Assert.That(simple.Created, Is.True);
            Assert.That(simple.AggregateVersion, Is.EqualTo(1));

            seh = processResult[typeof(SimpleAtomicReadmodelWithSingleEventHandled)] as SimpleAtomicReadmodelWithSingleEventHandled;
            Assert.That(seh.TouchCount, Is.EqualTo(0));
            Assert.That(seh.AggregateVersion, Is.EqualTo(1));
        }

        [Test]
        public async Task Empty_readmodel_handling_for_multiple_projection()
        {
            var c1 = await GenerateSomeChangesetsAndReturnLatestsChangeset().ConfigureAwait(false);
            await GenerateTouchedEvent().ConfigureAwait(false);

            //ok we need to check that events are not mixed.
            var sut = _container.Resolve<ILiveAtomicReadModelProcessorEnhanced>();
            var processResult = await sut.ProcessAsync(
                new Type[] { typeof(SimpleTestAtomicReadModel), typeof(SimpleAtomicReadmodelWithSingleEventHandled) },
                "not_existing_aggregate_id",
                Int32.MaxValue).ConfigureAwait(false);

            Assert.Null(processResult.Get<SimpleTestAtomicReadModel>());
            Assert.Null(processResult.Get<SimpleAtomicReadmodelWithSingleEventHandled>());
        }

        [Test]
        public async Task Project_more_than_one_readmodel_up_until_certain_date()
        {
            var c1 = await GenerateSomeChangesetsAndReturnLatestsChangeset().ConfigureAwait(false);

            //First block generates 3 events and two touch this is another touch 3 touch and version 4 (creation event)
            await GenerateTouchedEvent().ConfigureAwait(false);

            //we are at version 4 now put events in the future.

            DateTime future1 = DateTime.UtcNow.AddMinutes(1);
            await GenerateTouchedEvent(timestamp: future1);

            DateTime future2 = DateTime.UtcNow.AddMinutes(3);
            await GenerateTouchedEvent(timestamp: future2);

            //ok we need to check that events are not mixed.
            var sut = _container.Resolve<ILiveAtomicReadModelProcessorEnhanced>();
            DomainEvent firstEvent = (DomainEvent)c1.Events[0];

            //Process until now, we should have everythign up to latest two events.
            var processedResult = await sut.ProcessAsyncUntilUtcTimestamp<SimpleTestAtomicReadModel>(
                new Type[] { typeof(SimpleTestAtomicReadModel), typeof(SimpleAtomicReadmodelWithSingleEventHandled) },
                firstEvent.AggregateId.AsString(),
                DateTime.UtcNow).ConfigureAwait(false);

            var processed = processedResult.Get<SimpleTestAtomicReadModel>();
            Assert.That(processed.TouchCount, Is.EqualTo(3));
            Assert.That(processed.AggregateVersion, Is.EqualTo(4));

            var processedSimple = processedResult.Get<SimpleAtomicReadmodelWithSingleEventHandled>();
            Assert.That(processedSimple.TouchCount, Is.EqualTo(3));
            Assert.That(processedSimple.AggregateVersion, Is.EqualTo(4));

            //project up to future 1
            processedResult = await sut.ProcessAsyncUntilUtcTimestamp<SimpleTestAtomicReadModel>(
                  new Type[] { typeof(SimpleTestAtomicReadModel), typeof(SimpleAtomicReadmodelWithSingleEventHandled) },
                  firstEvent.AggregateId.AsString(),
                  future1).ConfigureAwait(false);

            processed = processedResult.Get<SimpleTestAtomicReadModel>();
            Assert.That(processed.TouchCount, Is.EqualTo(4));
            Assert.That(processed.AggregateVersion, Is.EqualTo(5));

            processedSimple = processedResult.Get<SimpleAtomicReadmodelWithSingleEventHandled>();
            Assert.That(processedSimple.TouchCount, Is.EqualTo(4));
            Assert.That(processedSimple.AggregateVersion, Is.EqualTo(5));

            //project up to future 2
            processedResult = await sut.ProcessAsyncUntilUtcTimestamp<SimpleTestAtomicReadModel>(
                  new Type[] { typeof(SimpleTestAtomicReadModel), typeof(SimpleAtomicReadmodelWithSingleEventHandled) },
                  firstEvent.AggregateId.AsString(),
                  future2).ConfigureAwait(false);

            processed = processedResult.Get<SimpleTestAtomicReadModel>();
            Assert.That(processed.TouchCount, Is.EqualTo(5));
            Assert.That(processed.AggregateVersion, Is.EqualTo(6));

            processedSimple = processedResult.Get<SimpleAtomicReadmodelWithSingleEventHandled>();
            Assert.That(processedSimple.TouchCount, Is.EqualTo(5));
            Assert.That(processedSimple.AggregateVersion, Is.EqualTo(6));

            //project up to the max
            processedResult = await sut.ProcessAsyncUntilUtcTimestamp<SimpleTestAtomicReadModel>(
                  new Type[] { typeof(SimpleTestAtomicReadModel), typeof(SimpleAtomicReadmodelWithSingleEventHandled) },
                  firstEvent.AggregateId.AsString(),
                  DateTime.UtcNow.AddYears(2000)).ConfigureAwait(false);

            processed = processedResult.Get<SimpleTestAtomicReadModel>();
            Assert.That(processed.TouchCount, Is.EqualTo(5));
            Assert.That(processed.AggregateVersion, Is.EqualTo(6));

            processedSimple = processedResult.Get<SimpleAtomicReadmodelWithSingleEventHandled>();
            Assert.That(processedSimple.TouchCount, Is.EqualTo(5));
            Assert.That(processedSimple.AggregateVersion, Is.EqualTo(6));
        }

        [Test]
        public async Task Project_multiple_readmodel_up_until_certain_checkpoint_number()
        {
            var c1 = await GenerateSomeChangesetsAndReturnLatestsChangeset().ConfigureAwait(false);
            var c2 = await GenerateTouchedEvent().ConfigureAwait(false);

            //ok we need to check that events are not mixed.
            var sut = _container.Resolve<ILiveAtomicReadModelProcessorEnhanced>();
            DomainEvent firstEvent = (DomainEvent)c1.Events[0];
            var processedResult = await sut.ProcessAsyncUntilChunkPosition(
                new Type[] { typeof(SimpleTestAtomicReadModel), typeof(SimpleAtomicReadmodelWithSingleEventHandled) },
                firstEvent.AggregateId.AsString(),
                firstEvent.CheckpointToken).ConfigureAwait(false);

            var processed = processedResult.Get<SimpleTestAtomicReadModel>();
            Assert.That(processed.TouchCount, Is.EqualTo(2));
            Assert.That(processed.AggregateVersion, Is.EqualTo(3));

            var processedSimple = processedResult.Get<SimpleAtomicReadmodelWithSingleEventHandled>();
            Assert.That(processedSimple.TouchCount, Is.EqualTo(2));
            Assert.That(processedSimple.AggregateVersion, Is.EqualTo(3));

            firstEvent = (DomainEvent)c2.Events[0];
            processedResult = await sut.ProcessAsyncUntilChunkPosition(
                new Type[] { typeof(SimpleTestAtomicReadModel), typeof(SimpleAtomicReadmodelWithSingleEventHandled) },
                firstEvent.AggregateId.AsString(),
                firstEvent.CheckpointToken).ConfigureAwait(false);

            processed = processedResult.Get<SimpleTestAtomicReadModel>();
            Assert.That(processed.TouchCount, Is.EqualTo(3));
            Assert.That(processed.AggregateVersion, Is.EqualTo(4));

            processedSimple = processedResult.Get<SimpleAtomicReadmodelWithSingleEventHandled>();
            Assert.That(processedSimple.TouchCount, Is.EqualTo(3));
            Assert.That(processedSimple.AggregateVersion, Is.EqualTo(4));
        }

        #endregion

        #region Cache usage

        /// <summary>
        /// Full scenario in cache usage for a single readmodel based on checkpoint (position)
        /// </summary>
        [Test]
        public async Task Project_up_until_certain_global_position_with_cache()
        {
            //we start creating changeset, than change id so the globa id is not the same as the
            //aggregate version id, be sure that the test is ok
            var c0 = await GenerateSomeChangesetsAndReturnLatestsChangeset().ConfigureAwait(false);
            _aggregateIdSeed++;
            _aggregateVersion = 1;

            //remember this will generate 2 touch event
            var c1 = await GenerateSomeChangesetsAndReturnLatestsChangeset().ConfigureAwait(false);

            var c2 = await GenerateTouchedEvent().ConfigureAwait(false);
            var c3 = await GenerateTouchedEvent().ConfigureAwait(false);
            await GenerateTouchedEvent().ConfigureAwait(false);

            //ok we need to check that events are not mixed.
            var sut = (LiveAtomicReadModelProcessor)_container.Resolve<ILiveAtomicReadModelProcessor>();
            //forcefully substitute the cache.
            sut.LiveAtomicreadmodelProcessorCache = Substitute.For<ILiveAtomicreadmodelProcessorCache>();

            //Process until now, we should have everythign up to latest two events.
            string id = c1.GetIdentity().AsString();
            var processed = await sut.ProcessAsyncUntilChunkPosition<SimpleTestAtomicReadModel>(
                id,
                c3.GetChunkPosition()).ConfigureAwait(false);

            Assert.That(processed.TouchCount, Is.EqualTo(4));
            Assert.That(processed.AggregateVersion, Is.EqualTo(c3.AggregateVersion));

            //now verify that cache has NOT been used, all 5 changeset are processed.
            Assert.That(processed.ChangesetProcessed, Is.EqualTo(5));

            //now kick in the cache using the old object
            sut.LiveAtomicreadmodelProcessorCache.GetReadmodelAtCheckpointAsync<SimpleTestAtomicReadModel>(
                c1.GetIdentity().AsString(),
                c3.GetChunkPosition()).Returns(Task.FromResult(processed.Clone()));

            var processed2 = await sut.ProcessAsyncUntilChunkPosition<SimpleTestAtomicReadModel>(
                c1.GetIdentity().AsString(),
                c3.GetChunkPosition()).ConfigureAwait(false);

            Assert.That(processed2.TouchCount, Is.EqualTo(4));
            Assert.That(processed2.AggregateVersion, Is.EqualTo(c3.AggregateVersion));

            //now verify that cache is used and no changeset are processed.
            Assert.That(processed2.ChangesetProcessed, Is.EqualTo(0));

            //now create another readmodel projected ad c1
            processed = await sut.ProcessAsyncUntilChunkPosition<SimpleTestAtomicReadModel>(
               id,
               c2.GetChunkPosition()).ConfigureAwait(false);
            sut.LiveAtomicreadmodelProcessorCache = Substitute.For<ILiveAtomicreadmodelProcessorCache>();

            //now a cache projected at checpoint 2
            sut.LiveAtomicreadmodelProcessorCache.GetReadmodelAtCheckpointAsync<SimpleTestAtomicReadModel>(
                c1.GetIdentity().AsString(),
                c3.GetChunkPosition()).Returns(Task.FromResult(processed.Clone()));

            var processed3 = await sut.ProcessAsyncUntilChunkPosition<SimpleTestAtomicReadModel>(
               c1.GetIdentity().AsString(),
               c3.GetChunkPosition()).ConfigureAwait(false);

            Assert.That(processed3.TouchCount, Is.EqualTo(4));
            Assert.That(processed3.AggregateVersion, Is.EqualTo(c3.AggregateVersion));

            //now verify that cache is used and only latest changeset was used.
            Assert.That(processed3.ChangesetProcessed, Is.EqualTo(1));
        }

        /// <summary>
        /// Full scenario in cache usage for a single readmodel based on checkpoint (position)
        /// </summary>
        [Test]
        public async Task Project_up_until_certain_aggregate_version_with_cache()
        {
            //we start creating changeset, than change id so the globa id is not the same as the
            //aggregate version id, be sure that the test is ok
            var c0 = await GenerateSomeChangesetsAndReturnLatestsChangeset().ConfigureAwait(false);
            _aggregateIdSeed++;
            _aggregateVersion = 1;

            //remember this will generate 2 touch event
            var c1 = await GenerateSomeChangesetsAndReturnLatestsChangeset().ConfigureAwait(false);

            await GenerateTouchedEvent().ConfigureAwait(false);
            await GenerateTouchedEvent().ConfigureAwait(false);
            await GenerateTouchedEvent().ConfigureAwait(false);

            //ok we need to check that events are not mixed.
            var sut = (LiveAtomicReadModelProcessor)_container.Resolve<ILiveAtomicReadModelProcessor>();
            //forcefully substitute the cache.
            sut.LiveAtomicreadmodelProcessorCache = Substitute.For<ILiveAtomicreadmodelProcessorCache>();

            //Process until now, we should have everythign up to latest two events.
            string id = c1.GetIdentity().AsString();

            //This version of the call will process up to version of an aggregate.
            //we will process up to version 4, this comprehend first creation and three touches
            var processed = await sut.ProcessAsync<SimpleTestAtomicReadModel>(id, 4).ConfigureAwait(false);

            Assert.That(processed.TouchCount, Is.EqualTo(3));
            Assert.That(processed.AggregateVersion, Is.EqualTo(4));

            //now verify that cache has NOT been used, all 4 changeset are processed.
            Assert.That(processed.ChangesetProcessed, Is.EqualTo(4));

            //now kick in the cache using the old object
            sut.LiveAtomicreadmodelProcessorCache.GetReadmodelAtVersionAsync<SimpleTestAtomicReadModel>(id, 4).Returns(Task.FromResult(processed.Clone()));

            var processed2 = await sut.ProcessAsync<SimpleTestAtomicReadModel>(id, 4).ConfigureAwait(false);

            Assert.That(processed2.TouchCount, Is.EqualTo(3));
            Assert.That(processed2.AggregateVersion, Is.EqualTo(4));

            //now verify that cache is used and no changeset are processed.
            Assert.That(processed2.ChangesetProcessed, Is.EqualTo(0));

            //now create another readmodel projected ad c1
            processed = await sut.ProcessAsync<SimpleTestAtomicReadModel>(id, 3).ConfigureAwait(false);

            //now a cache projected at checpoint 2
            sut.LiveAtomicreadmodelProcessorCache = Substitute.For<ILiveAtomicreadmodelProcessorCache>();
            sut.LiveAtomicreadmodelProcessorCache.GetReadmodelAtVersionAsync<SimpleTestAtomicReadModel>(id, 4).Returns(Task.FromResult(processed.Clone()));

            var processed3 = await sut.ProcessAsync<SimpleTestAtomicReadModel>(id, 4).ConfigureAwait(false);

            Assert.That(processed3.TouchCount, Is.EqualTo(3));
            Assert.That(processed3.AggregateVersion, Is.EqualTo(4));

            //now verify that cache is used and only latest changeset was used.
            Assert.That(processed3.ChangesetProcessed, Is.EqualTo(1));
        }

        [Test]
        public async Task If_cache_has_old_readmodel_version_cache_is_not_used()
        {
            //remember this will generate 2 touch event
            var c1 = await GenerateSomeChangesetsAndReturnLatestsChangeset().ConfigureAwait(false);

            await GenerateTouchedEvent().ConfigureAwait(false);
            var c3 = await GenerateTouchedEvent().ConfigureAwait(false);
            await GenerateTouchedEvent().ConfigureAwait(false);

            //ok we need to check that events are not mixed.
            var sut = (LiveAtomicReadModelProcessor)_container.Resolve<ILiveAtomicReadModelProcessor>();
            //forcefully substitute the cache.
            sut.LiveAtomicreadmodelProcessorCache = Substitute.For<ILiveAtomicreadmodelProcessorCache>();

            //Process until now, we should have everythign up to latest two events.
            string id = c1.GetIdentity().AsString();
            var processed = await sut.ProcessAsyncUntilChunkPosition<SimpleTestAtomicReadModel>(
                id,
                c3.GetChunkPosition()).ConfigureAwait(false);

            Assert.That(processed.TouchCount, Is.EqualTo(4));
            Assert.That(processed.AggregateVersion, Is.EqualTo(c3.AggregateVersion));

            //now verify that cache has NOT been used, all 5 changeset are processed.
            Assert.That(processed.ChangesetProcessed, Is.EqualTo(5));

            //now kick in the cache using the old object
            processed.InstanceFakeSignature = -1; //different signature.
            sut.LiveAtomicreadmodelProcessorCache.GetReadmodelAtCheckpointAsync<SimpleTestAtomicReadModel>(
                c1.GetIdentity().AsString(),
                c3.GetChunkPosition()).Returns(Task.FromResult(processed.Clone()));

            var processed2 = await sut.ProcessAsyncUntilChunkPosition<SimpleTestAtomicReadModel>(
                c1.GetIdentity().AsString(),
                c3.GetChunkPosition()).ConfigureAwait(false);

            Assert.That(processed2.TouchCount, Is.EqualTo(4));
            Assert.That(processed2.AggregateVersion, Is.EqualTo(c3.AggregateVersion));

            //now verify that cache is used and no changeset are processed.
            Assert.That(processed2.ChangesetProcessed, Is.EqualTo(5), "We need to re-project all because version of readmodel is not correct.");
        }

        [Test]
        public async Task Verify_write_cache_when_finished_rehyidrating_readmodel()
        {
            //we start creating changeset, than change id so the globa id is not the same as the
            //aggregate version id, be sure that the test is ok
            var c0 = await GenerateSomeChangesetsAndReturnLatestsChangeset().ConfigureAwait(false);
            _aggregateIdSeed++;
            _aggregateVersion = 1;

            //remember this will generate 2 touch event
            var c1 = await GenerateSomeChangesetsAndReturnLatestsChangeset().ConfigureAwait(false);

            var c2 = await GenerateTouchedEvent().ConfigureAwait(false);
            var c3 = await GenerateTouchedEvent().ConfigureAwait(false);
            await GenerateTouchedEvent().ConfigureAwait(false);

            //ok we need to check that events are not mixed.
            var sut = (LiveAtomicReadModelProcessor)_container.Resolve<ILiveAtomicReadModelProcessor>();
            //forcefully substitute the cache.
            sut.LiveAtomicreadmodelProcessorCache = Substitute.For<ILiveAtomicreadmodelProcessorCache>();

            //Process until now, we should have everythign up to latest two events.
            string id = c1.GetIdentity().AsString();
            var processed = await sut.ProcessAsyncUntilChunkPosition<SimpleTestAtomicReadModel>(
                id,
                c3.GetChunkPosition()).ConfigureAwait(false);

            //VErify with Nsubstitute taht we called save for the readmodel.
            await sut.LiveAtomicreadmodelProcessorCache.Received(1).SaveReadmodelInCacheAsync<SimpleTestAtomicReadModel>(
                Arg.Is<SimpleTestAtomicReadModel>(v => v.ProjectedPosition == c3.GetChunkPosition()));
        }

        [Test]
        public async Task Verify_write_cache_when_finished_rehyidrating_readmodel_with_version()
        {
            //we start creating changeset, than change id so the globa id is not the same as the
            //aggregate version id, be sure that the test is ok
            var c0 = await GenerateSomeChangesetsAndReturnLatestsChangeset().ConfigureAwait(false);
            _aggregateIdSeed++;
            _aggregateVersion = 1;

            //remember this will generate 2 touch event
            var c1 = await GenerateSomeChangesetsAndReturnLatestsChangeset().ConfigureAwait(false);

            var c2 = await GenerateTouchedEvent().ConfigureAwait(false);
            var c3 = await GenerateTouchedEvent().ConfigureAwait(false);
            await GenerateTouchedEvent().ConfigureAwait(false);

            //ok we need to check that events are not mixed.
            var sut = (LiveAtomicReadModelProcessor)_container.Resolve<ILiveAtomicReadModelProcessor>();
            //forcefully substitute the cache.
            sut.LiveAtomicreadmodelProcessorCache = Substitute.For<ILiveAtomicreadmodelProcessorCache>();

            //Process until now, we should have everythign up to latest two events.
            string id = c1.GetIdentity().AsString();
            var processed = await sut.ProcessAsync<SimpleTestAtomicReadModel>(
                id,
                c3.AggregateVersion).ConfigureAwait(false);

            //VErify with Nsubstitute taht we called save for the readmodel.
            await sut.LiveAtomicreadmodelProcessorCache.Received(1).SaveReadmodelInCacheAsync<SimpleTestAtomicReadModel>(
                Arg.Is<SimpleTestAtomicReadModel>(v => v.ProjectedPosition == c3.GetChunkPosition()));
        }
        #endregion
    }
}
