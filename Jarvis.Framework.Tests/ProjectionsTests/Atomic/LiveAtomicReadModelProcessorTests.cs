using Fasterflect;
using Jarvis.Framework.Shared.Events;
using Jarvis.Framework.Shared.ReadModel.Atomic;
using Jarvis.Framework.Tests.EngineTests;
using Jarvis.Framework.Tests.ProjectionsTests.Atomic.Support;
using NUnit.Framework;
using System;
using System.Linq;
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
            var processed = await sut.ProcessUntilChunkPositionAsync<SimpleTestAtomicReadModel>(
                  "SampleAggregate_123123123",
                  0L).ConfigureAwait(false);

            Assert.That(processed, Is.Null, "Aggregate does not exists at that checkpoint, we expect null");

            processed = await sut.ProcessAsync<SimpleTestAtomicReadModel>(
               "SampleAggregate_123123123",
               0L).ConfigureAwait(false);

            Assert.That(processed, Is.Null, "Aggregate does not exists at that checkpoint, we expect null");

            processed = await sut.ProcessUntilUtcTimestampAsync<SimpleTestAtomicReadModel>(
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
            var processed = await sut.ProcessUntilChunkPositionAsync<SimpleTestAtomicReadModel>(
                firstEvent.AggregateId.AsString(),
                firstEvent.CheckpointToken).ConfigureAwait(false);

            Assert.That(processed.TouchCount, Is.EqualTo(2));
            Assert.That(processed.AggregateVersion, Is.EqualTo(3));

            firstEvent = (DomainEvent)c2.Events[0];
            processed = await sut.ProcessUntilChunkPositionAsync<SimpleTestAtomicReadModel>(
                  firstEvent.AggregateId.AsString(),
                  0L).ConfigureAwait(false);

            Assert.That(processed, Is.Null, "Aggregate does not exists at that checkpoint, we expect null");

            processed = await sut.ProcessAsync<SimpleTestAtomicReadModel>(
               firstEvent.AggregateId.AsString(),
               0L).ConfigureAwait(false);

            Assert.That(processed, Is.Null, "Aggregate does not exists at that checkpoint, we expect null");

            processed = await sut.ProcessUntilUtcTimestampAsync<SimpleTestAtomicReadModel>(
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
            var processed = await sut.ProcessUntilChunkPositionAsync<SimpleTestAtomicReadModel>(
                firstEvent.AggregateId.AsString(),
                firstEvent.CheckpointToken).ConfigureAwait(false);

            Assert.That(processed.TouchCount, Is.EqualTo(2));
            Assert.That(processed.AggregateVersion, Is.EqualTo(3));

            firstEvent = (DomainEvent)c2.Events[0];
            processed = await sut.ProcessUntilChunkPositionAsync<SimpleTestAtomicReadModel>(
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
            var processed = await sut.ProcessUntilUtcTimestampAsync<SimpleTestAtomicReadModel>(
                firstEvent.AggregateId.AsString(),
                DateTime.UtcNow).ConfigureAwait(false);

            Assert.That(processed.TouchCount, Is.EqualTo(3));
            Assert.That(processed.AggregateVersion, Is.EqualTo(4));

            //project up to future 1
            processed = await sut.ProcessUntilUtcTimestampAsync<SimpleTestAtomicReadModel>(
                  firstEvent.AggregateId.AsString(),
                  future1).ConfigureAwait(false);

            Assert.That(processed.TouchCount, Is.EqualTo(4));
            Assert.That(processed.AggregateVersion, Is.EqualTo(5));

            //project up to future 2
            processed = await sut.ProcessUntilUtcTimestampAsync<SimpleTestAtomicReadModel>(
                  firstEvent.AggregateId.AsString(),
                  future2).ConfigureAwait(false);

            Assert.That(processed.TouchCount, Is.EqualTo(5));
            Assert.That(processed.AggregateVersion, Is.EqualTo(6));

            //project up to the max
            processed = await sut.ProcessUntilUtcTimestampAsync<SimpleTestAtomicReadModel>(
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
        public async Task Catchup_process_until_faulted()
        {
            await GenerateSomeChangesetsAndReturnLatestsChangeset().ConfigureAwait(false);
            var c2 = await GenerateSomeChangesetsAndReturnLatestsChangeset().ConfigureAwait(false);
            var c3 = await GenerateTouchedEvent().ConfigureAwait(false);
            var c4 = await GenerateTouchedEvent().ConfigureAwait(false);

            //Arrange: manually process some events in readmodel
            var firstEvent = (DomainEvent)c2.Events[0];
            var sut = _container.Resolve<ILiveAtomicReadModelProcessor>();
            var rm = await sut.ProcessAsync<SimpleTestAtomicReadModel>(
                firstEvent.AggregateId.AsString(),
                c2.AggregateVersion).ConfigureAwait(false);

            Assert.That(rm.AggregateVersion, Is.EqualTo(c2.AggregateVersion));
            var touchCount = rm.TouchCount;

            var actualMaxTouchCount = SimpleTestAtomicReadModel.TouchMax;
            SimpleTestAtomicReadModel.TouchMax = touchCount + 1;
            try
            {
                //Act, ask to catchup events, we expect c3 to be projected but c4 no because it will exceed touchmax     
                await sut.CatchupAsync(rm).ConfigureAwait(false);
                Assert.Fail("We need an exception to be trhown");
            }
            catch (Exception)
            {
                Assert.That(rm.AggregateVersion, Is.EqualTo(c4.AggregateVersion), "Changeset needs to be updated");
                Assert.That(rm.TouchCount, Is.EqualTo(touchCount + 1), "Event is not processed");
            }
            finally
            {
                SimpleTestAtomicReadModel.TouchMax = actualMaxTouchCount;
            }
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

        #region Conditioned projection

        [Test]
        public void Project_new_up_to_condition_parameterCheck()
        {
            var sut = _container.Resolve<ILiveAtomicReadModelProcessorEnhanced>();

            //At least one the function must be present.
            Assert.ThrowsAsync<ArgumentException>( async () => await sut.ProcessUntilAsync<SimpleTestAtomicReadModel>(
                "Document_1",
                readModel: null,
                changesetConditionEvaluator: null,
                readModelConditionEvaluator: null));

            //Id can be null
            Assert.ThrowsAsync<ArgumentNullException>(async () => await sut.ProcessUntilAsync<SimpleTestAtomicReadModel>(
                null,
                readModel: null,
                changesetConditionEvaluator: (changeset, rm, ct) => Task.FromResult(true),
                readModelConditionEvaluator: null));
        }

        [Test]
        public async Task Project_new_up_to_condition_on_readmodel()
        {
            //Generate some changed events
            var c1 = await GenerateSomeChangesetsAndReturnLatestsChangeset().ConfigureAwait(false);
            await GenerateTouchedEvent().ConfigureAwait(false);

            //ok we need to check that events are not mixed.
            var sut = _container.Resolve<ILiveAtomicReadModelProcessorEnhanced>();
            var aggregateId = ((DomainEvent)c1.Events[0]).AggregateId;
            var processResult = await sut.ProcessUntilAsync< SimpleTestAtomicReadModel>(
                aggregateId,
                readModel: null,
                changesetConditionEvaluator: null,
                //Stop immediately when touch count is greater than 1
                (rm, _) => Task.FromResult( rm.TouchCount > 1)).ConfigureAwait(false);

            Assert.That(processResult.TouchCount, Is.EqualTo(2));
        }

        [Test]
        public async Task Project_starting_from_specific_version()
        {
            //Generate some changed events
            var c1 = await GenerateSomeChangesetsAndReturnLatestsChangeset().ConfigureAwait(false);
            await GenerateTouchedEvent().ConfigureAwait(false);

            //ok we need to check that events are not mixed.
            var sut = _container.Resolve<ILiveAtomicReadModelProcessorEnhanced>();
            var aggregateId = ((DomainEvent)c1.Events[0]).AggregateId;
            var processResult = await sut.ProcessUntilAsync<SimpleTestAtomicReadModel>(
                aggregateId,
                readModel: null,
                changesetConditionEvaluator: null,
                //Stop immediately when touch count is greater than 1
                (rm, _) => Task.FromResult(rm.TouchCount > 1)).ConfigureAwait(false);

            //now that I have the readmodel I'll continue projecting
            Assert.That(processResult.TouchCount, Is.EqualTo(2));

            processResult.SetPropertyValue("TouchCount", 1213); //Set a really high value to check that we will start from this
            var reProjected = await sut.ProcessUntilAsync<SimpleTestAtomicReadModel>(
                aggregateId,
                readModel: processResult,
                changesetConditionEvaluator: (c, rm, _) => Task.FromResult( c.Events.FirstOrDefault() is SampleAggregateInvalidated),
                //Stop immediately when touch count is greater than 1
                null).ConfigureAwait(false);

            Assert.That(object.ReferenceEquals(processResult, reProjected));
            Assert.That(processResult.TouchCount, Is.EqualTo(1214));
        }

        [Test]
        public async Task Stop_immediately_before_we_reach_touch_count()
        {
            //Generate some changed events
            var c1 = await GenerateSomeChangesetsAndReturnLatestsChangeset().ConfigureAwait(false);
            await GenerateTouchedEvent().ConfigureAwait(false);
            //Now invalidate, then finally add another touched event
            await GenerateInvalidatedEvent().ConfigureAwait(false);
            await GenerateTouchedEvent().ConfigureAwait(false);

            //ok we need to check that events are not mixed.
            var sut = _container.Resolve<ILiveAtomicReadModelProcessorEnhanced>();
            var aggregateId = ((DomainEvent)c1.Events[0]).AggregateId;
            var processResult = await sut.ProcessUntilAsync<SimpleTestAtomicReadModel>(
                aggregateId,
                readModel: null,
                changesetConditionEvaluator: (changeset, readmodel, _) =>
                {
                    if (changeset.Events.FirstOrDefault() is SampleAggregateTouched && readmodel.Invalidated)
                    {
                        //only if the readmodel is invalidated we stop the processing.
                        return Task.FromResult(true);
                    }

                    return Task.FromResult(false);
                },
                //Stop immediately when touch count is greater than 1
                null);

            Assert.That(processResult.Invalidated, Is.True);
            Assert.That(processResult.TouchCount, Is.EqualTo(3));
        }

        [Test]
        public async Task First_condition_win()
        {
            //Generate some changed events
            var c1 = await GenerateSomeChangesetsAndReturnLatestsChangeset().ConfigureAwait(false);
            await GenerateTouchedEvent().ConfigureAwait(false);
            //Now invalidate, then finally add another touched event
            await GenerateInvalidatedEvent().ConfigureAwait(false);
            await GenerateTouchedEvent().ConfigureAwait(false);

            //ok we need to check that events are not mixed.
            var sut = _container.Resolve<ILiveAtomicReadModelProcessorEnhanced>();
            var aggregateId = ((DomainEvent)c1.Events[0]).AggregateId;
            var processResult = await sut.ProcessUntilAsync<SimpleTestAtomicReadModel>(
                aggregateId,
                readModel: null,
                changesetConditionEvaluator: (changeset, readmodel, _) => Task.FromResult(changeset.AggregateVersion > 2),
                (rm, _) => Task.FromResult(rm.TouchCount == 1) //this will win
                );

            Assert.That(processResult.Invalidated, Is.False);
            Assert.That(processResult.TouchCount, Is.EqualTo(1));

            processResult = await sut.ProcessUntilAsync<SimpleTestAtomicReadModel>(
              aggregateId,
              readModel: null,
              changesetConditionEvaluator: (changeset, readmodel, _) => Task.FromResult(changeset.AggregateVersion > 1),
              (rm, _) => Task.FromResult(rm.TouchCount < 4) //this will win
              );

            Assert.That(processResult.Invalidated, Is.False);
            Assert.That(processResult.TouchCount, Is.EqualTo(0));
        }

        [Test]
        public async Task Condition_that_will_never_be_met_project_everything()
        {
            //Generate some changed events
            var c1 = await GenerateSomeChangesetsAndReturnLatestsChangeset().ConfigureAwait(false);
            await GenerateTouchedEvent().ConfigureAwait(false);

            //ok we need to check that events are not mixed.
            var sut = _container.Resolve<ILiveAtomicReadModelProcessorEnhanced>();
            var aggregateId = ((DomainEvent)c1.Events[0]).AggregateId;
            var processResult = await sut.ProcessUntilAsync<SimpleTestAtomicReadModel>(
                aggregateId,
                readModel: null,
                changesetConditionEvaluator: null,
                //this will never stop
                (rm, _) => Task.FromResult(rm.TouchCount > 234234234)).ConfigureAwait(false);

            //The condition was never met, we will process everything up to the end of the stream.
            Assert.That(processResult.TouchCount, Is.EqualTo(3));
        }
 
        #endregion

        #region Multi aggregate

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

            NUnit.Framework.Legacy.ClassicAssert.Null(processResult.Get<SimpleTestAtomicReadModel>());
            NUnit.Framework.Legacy.ClassicAssert.Null(processResult.Get<SimpleAtomicReadmodelWithSingleEventHandled>());
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
            var processedResult = await sut.ProcessUntilUtcTimestampAsync<SimpleTestAtomicReadModel>(
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
            processedResult = await sut.ProcessUntilUtcTimestampAsync<SimpleTestAtomicReadModel>(
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
            processedResult = await sut.ProcessUntilUtcTimestampAsync<SimpleTestAtomicReadModel>(
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
            processedResult = await sut.ProcessUntilUtcTimestampAsync<SimpleTestAtomicReadModel>(
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
            var processedResult = await sut.ProcessUntilChunkPositionAsync(
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
            processedResult = await sut.ProcessUntilChunkPositionAsync(
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
    }
}
