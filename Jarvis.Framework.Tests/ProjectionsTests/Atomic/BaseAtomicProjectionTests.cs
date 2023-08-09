﻿using Castle.MicroKernel.Registration;
using Jarvis.Framework.Kernel.ProjectionEngine.Atomic;
using Jarvis.Framework.Kernel.ProjectionEngine.Atomic.Support;
using Jarvis.Framework.Shared.Events;
using Jarvis.Framework.Shared.Helpers;
using Jarvis.Framework.Shared.ReadModel.Atomic;
using Jarvis.Framework.Tests.EngineTests;
using Jarvis.Framework.Tests.ProjectionsTests.Atomic.Support;
using NStore.Domain;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Jarvis.Framework.Tests.ProjectionsTests.Atomic
{
    [TestFixture]
    public class BaseAtomicProjectionTests : AtomicProjectionEngineTestBase
    {
        [Test]
        public void Smoke_register()
        {
            var sut = _container.Resolve<AtomicProjectionEngine>();
            sut.RegisterAtomicReadModel(typeof(SimpleTestAtomicReadModel));
        }

        [Test]
        public async Task Verify_basic_consumption_of_single_event()
        {
            AtomicReadmodelProjectorHelper<SimpleTestAtomicReadModel> sut =
                _container.Resolve<AtomicReadmodelProjectorHelper<SimpleTestAtomicReadModel>>();

            var changeset = await GenerateCreatedEvent().ConfigureAwait(false);
            await sut.Handle(lastUsedPosition, changeset, changeset.GetIdentity()).ConfigureAwait(false);

            //A readmodel should be created.
            var firstEvent = changeset.Events[0] as DomainEvent;
            var rm = await _collection.FindOneByIdAsync(firstEvent.AggregateId).ConfigureAwait(false);
            Assert.That(rm.ProjectedPosition, Is.EqualTo(firstEvent.CheckpointToken));
            Assert.That(rm.TouchCount, Is.EqualTo(0));
        }

        [Test]
        public async Task Verify_basic_consumption_of_two_events()
        {
            AtomicReadmodelProjectorHelper<SimpleTestAtomicReadModel> sut =
                _container.Resolve<AtomicReadmodelProjectorHelper<SimpleTestAtomicReadModel>>();

            var changeset = await GenerateCreatedEvent().ConfigureAwait(false);
            await sut.Handle(lastUsedPosition, changeset, changeset.GetIdentity()).ConfigureAwait(false);

            var touchChangeset = await GenerateTouchedEvent().ConfigureAwait(false);
            await sut.Handle(lastUsedPosition, touchChangeset, touchChangeset.GetIdentity()).ConfigureAwait(false);

            //A readmodel should be created.
            var firstEvent = touchChangeset.Events[0] as DomainEvent;
            var rm = await _collection.FindOneByIdAsync(firstEvent.AggregateId).ConfigureAwait(false);
            Assert.That(rm.ProjectedPosition, Is.EqualTo(firstEvent.CheckpointToken));
            Assert.That(rm.TouchCount, Is.EqualTo(1));
        }

        [Test]
        public async Task Register_and_project_basic_readmodel()
        {
            Changeset changeset = await GenerateSomeChangesetsAndReturnLatestsChangeset().ConfigureAwait(false);

            //And finally check if everything is projected
            await CreateSutAndStartProjectionEngineAsync().ConfigureAwait(false);

            //we need to wait to understand if it was projected
            GetTrackerAndWaitForChangesetToBeProjected("SimpleTestAtomicReadModel");

            //ok readmodel should be projected
            var evt = changeset.Events[0] as DomainEvent;
            var rm = await _collection.FindOneByIdAsync(evt.AggregateId).ConfigureAwait(false);
            Assert.That(rm.ProjectedPosition, Is.EqualTo(evt.CheckpointToken));
            Assert.That(rm.TouchCount, Is.EqualTo(2));
        }

        [Test]
        public async Task Not_consumed_event_does_generate_readmodel()
        {
            Changeset changeset = await GenerateSampleAggregateDerived1().ConfigureAwait(false);

            //And finally check if everything is projected
            await CreateSutAndStartProjectionEngineAsync().ConfigureAwait(false);

            //we need to wait to understand if it was projected
            GetTrackerAndWaitForChangesetToBeProjected("SimpleTestAtomicReadModel");

            //ok readmodel should be projected
            var evt = changeset.Events[0] as DomainEvent;
            var rm = await _collection.FindOneByIdAsync(evt.AggregateId).ConfigureAwait(false);
            Assert.That(rm, Is.Not.Null);
        }

        [Test]
        public async Task Project_readmodel_than_change_signature_verify_projection_of_new_events_fixes_readmodel()
        {
            //first step, create some events, project them
            await GenerateSomeChangesetsAndReturnLatestsChangeset().ConfigureAwait(false);

            //And finally check if everything is projected
            var sut = await CreateSutAndStartProjectionEngineAsync().ConfigureAwait(false);

            //we need to wait to understand if it was projected
            GetTrackerAndWaitForChangesetToBeProjected("SimpleTestAtomicReadModel");

            await sut.StopAsync().ConfigureAwait(false);

            //well generate another commit
            var changeset = await GenerateTouchedEvent().ConfigureAwait(false);

            //now simulate change of signature, recreate and restart another projection engine
            GenerateContainer(); //regenerate everything, we need to simulate a fresh start
            SimpleTestAtomicReadModel.FakeSignature = 2;

            await CreateSutAndStartProjectionEngineAsync().ConfigureAwait(false);
            //wait again for the next changeset to be projected.
            GetTrackerAndWaitForChangesetToBeProjected("SimpleTestAtomicReadModel");

            //ok readmodel should be projected and even if the recover thread is not started, it should be updated
            var evt = changeset.Events[0] as DomainEvent;
            var rm = await _collection.FindOneByIdAsync(evt.AggregateId).ConfigureAwait(false);
            Assert.That(rm.ProjectedPosition, Is.EqualTo(evt.CheckpointToken));
            Assert.That(rm.TouchCount, Is.EqualTo(6)); //we have 3 thouch,  multiplied by the fake signature
            Assert.That(rm.ReadModelVersion, Is.EqualTo(2));
        }

        [Test]
        public async Task Verify_flush_has_always_last_version_dispatched_even_if_readmodel_does_not_consume_event()
        {
            await GenerateSomeChangesetsAndReturnLatestsChangeset().ConfigureAwait(false);

            //Generate a changeset, that has an event that was not handled by the readmodel.
            await GenerateInvalidatedEvent().ConfigureAwait(false);

            //And finally check if everything is projected
            await CreateSutAndStartProjectionEngineAsync(1).ConfigureAwait(false);

            //we need to wait to understand if it was projected
            GetTrackerAndWaitForChangesetToBeProjected("SimpleTestAtomicReadModel");

            //now we need to monitor for checkpoint collection to be populated. 
            DateTime startWait = DateTime.UtcNow;
            while (DateTime.UtcNow.Subtract(startWait).TotalSeconds < 5)
            {
                try
                {
                    AtomicProjectionCheckpointManager newTracker = new AtomicProjectionCheckpointManager(_db);
                    //this new tracker reload from db
                    if (newTracker.GetCheckpoint("SimpleTestAtomicReadModel") == lastUsedPosition)
                    {
                        return; //test succeeded, it was saved.
                    }

                    Thread.Sleep(200);
                }
#pragma warning disable RCS1075 // Avoid empty catch clause that catches System.Exception.
                catch (Exception)
                {
                    //Ignore any error, the checkpoint coudl not be still saved. (we can improve the test reading directly the mongo collection)
                }
#pragma warning restore RCS1075 // Avoid empty catch clause that catches System.Exception.
            }

            //if we reach here, no flush was made...
            Assert.Fail("No flushing occurred");
        }

        [Test]
        public async Task Verify_flush_has_always_last_version_dispatched_even_with_events_of_other_aggregates()
        {
            await GenerateSomeChangesetsAndReturnLatestsChangeset().ConfigureAwait(false);

            //Generate a changeset, that has an event that was not handled by the readmodel.
            await GenerateAtomicAggregateCreatedEvent().ConfigureAwait(false);

            //And finally check if everything is projected
            await CreateSutAndStartProjectionEngineAsync(1).ConfigureAwait(false);

            //we need to wait to understand if it was projected
            GetTrackerAndWaitForChangesetToBeProjected("SimpleTestAtomicReadModel");

            //now we need to monitor for checkpoint collection to be populated. 
            DateTime startWait = DateTime.UtcNow;
            while (DateTime.UtcNow.Subtract(startWait).TotalSeconds < 5)
            {
                try
                {
                    AtomicProjectionCheckpointManager newTracker = new AtomicProjectionCheckpointManager(_db);
                    //this new tracker reload from db
                    if (newTracker.GetCheckpoint("SimpleTestAtomicReadModel") == lastUsedPosition)
                    {
                        return; //test succeeded, it was saved.
                    }

                    Thread.Sleep(200);
                }
#pragma warning disable RCS1075 // Avoid empty catch clause that catches System.Exception.
                catch (Exception)
                {
                    //Ignore any error, the checkpoint coudl not be still saved. (we can improve the test reading directly the mongo collection)
                }
#pragma warning restore RCS1075 // Avoid empty catch clause that catches System.Exception.
            }

            //if we reach here, no flush was made...
            Assert.Fail("No flushing occurred");
        }

        [Test]
        public async Task Verify_checkpoint_are_flushed()
        {
            await GenerateSomeChangesetsAndReturnLatestsChangeset().ConfigureAwait(false);

            //And finally check if everything is projected
            await CreateSutAndStartProjectionEngineAsync(1).ConfigureAwait(false);

            //we need to wait to understand if it was projected
            GetTrackerAndWaitForChangesetToBeProjected("SimpleTestAtomicReadModel");

            //now we need to monitor for checkpoint collection to be populated. 
            DateTime startWait = DateTime.UtcNow;
            while (DateTime.UtcNow.Subtract(startWait).TotalSeconds < 5)
            {
                try
                {
                    AtomicProjectionCheckpointManager newTracker = new AtomicProjectionCheckpointManager(_db);
                    //this new tracker reload from db
                    if (newTracker.GetCheckpoint("SimpleTestAtomicReadModel") == lastUsedPosition)
                    {
                        return; //test succeeded, it was saved.
                    }

                    Thread.Sleep(200);
                }
#pragma warning disable RCS1075 // Avoid empty catch clause that catches System.Exception.
                catch (Exception)
                {
                    //Ignore any error, the checkpoint coudl not be still saved. (we can improve the test reading directly the mongo collection)
                }
#pragma warning restore RCS1075 // Avoid empty catch clause that catches System.Exception.
            }

            //if we reach here, no flush was made...
            Assert.Fail("No flushing occurred");
        }

        [Test]
        public async Task Initializer_were_called_correctly()
        {
            _container.Register(
                Component.For<IAtomicReadModelInitializer>()
                .ImplementedBy<SimpleTestAtomicReadModelInitializer>());

            var sut = _container.Resolve<AtomicProjectionEngine>();
            await sut.StartAsync(100, 100).ConfigureAwait(false);
            //we need to verify that the index was created.

            var initializer = _container.ResolveAll<IAtomicReadModelInitializer>()
                .OfType<SimpleTestAtomicReadModelInitializer>()
                .Single();

            Assert.That(initializer.Initialized);
        }

        [Test]
        public async Task Verify_generation_of_notification_updated()
        {
            //Three events all generated in datbase
            await GenerateCreatedEvent().ConfigureAwait(false);
            var c2 = await GenerateTouchedEvent().ConfigureAwait(false);
            var c3 = await GenerateTouchedEvent().ConfigureAwait(false);

            //And finally check if everything is projected
            _sut = await CreateSutAndStartProjectionEngineAsync(autostart: false).ConfigureAwait(false);
            _sut.AtomicReadmodelNotifier = Substitute.For<IAtomicReadmodelNotifier>();
            await _sut.StartAsync(100, 100).ConfigureAwait(false);

            //wait that commit was projected
            GetTrackerAndWaitForChangesetToBeProjected("SimpleTestAtomicReadModel");

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            _sut.AtomicReadmodelNotifier
                .Received(1)
                .ReadmodelUpdatedAsync(
                    Arg.Any<IAtomicReadModel>(),
                    Arg.Is<Changeset>(c => c.GetChunkPosition() == c2.GetChunkPosition()));

            _sut.AtomicReadmodelNotifier
                .Received(1)
                .ReadmodelUpdatedAsync(
                    Arg.Any<IAtomicReadModel>(),
                    Arg.Is<Changeset>(c => c.GetChunkPosition() == c3.GetChunkPosition()));
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
        }

        [Test]
        public async Task Verify_generation_of_notification_inserted()
        {
            //Three events all generated in datbase
            var c1 = await GenerateCreatedEvent().ConfigureAwait(false);
            await GenerateTouchedEvent().ConfigureAwait(false);
            await GenerateTouchedEvent().ConfigureAwait(false);

            //And finally check if everything is projected
            _sut = await CreateSutAndStartProjectionEngineAsync(autostart: false).ConfigureAwait(false);
            _sut.AtomicReadmodelNotifier = Substitute.For<IAtomicReadmodelNotifier>();
            await _sut.StartAsync(100, 100).ConfigureAwait(false);

            //wait that commit was projected
            GetTrackerAndWaitForChangesetToBeProjected("SimpleTestAtomicReadModel");

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            _sut.AtomicReadmodelNotifier
                .Received(1)
                .ReadmodelCreatedAsync(
                    Arg.Any<IAtomicReadModel>(),
                    Arg.Is<Changeset>(c => c.GetChunkPosition() == c1.GetChunkPosition()));
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
        }

        [Test]
        public async Task Verify_notification_is_not_thrown_if_event_is_not_handled()
        {
            var evt = new SampleAggregateDerived1();
            SetBasePropertiesToEvent(evt, null);
            var changeset = await ProcessEvent(evt).ConfigureAwait(false);

            //And finally check if everything is projected
            _sut = await CreateSutAndStartProjectionEngineAsync(autostart: false).ConfigureAwait(false);
            _sut.AtomicReadmodelNotifier = Substitute.For<IAtomicReadmodelNotifier>();
            await _sut.StartAsync(100, 100).ConfigureAwait(false);

            //wait that commit was projected
            GetTrackerAndWaitForChangesetToBeProjected("SimpleTestAtomicReadModel");

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            _sut.AtomicReadmodelNotifier
                .DidNotReceive()
                .ReadmodelUpdatedAsync(Arg.Any<IAtomicReadModel>(), changeset);
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
        }

        [Test]
        public async Task Verify_version_and_position_are_updated_event_if_readmodel_does_not_consume_event()
        {
            var changeset = await GenerateSomeChangesetsAndReturnLatestsChangeset().ConfigureAwait(false);

            //Generate a changeset, that has an event that was not handled by the readmodel.
            var notHandledChangeset = await GenerateInvalidatedEvent().ConfigureAwait(false);

            //And finally check if everything is projected
            await CreateSutAndStartProjectionEngineAsync(1).ConfigureAwait(false);

            //we need to wait to understand if it was projected
            GetTrackerAndWaitForChangesetToBeProjected("SimpleTestAtomicReadModel", waitTimeInSeconds: 10);

            var evt = changeset.Events[0] as DomainEvent;
            var rm = await _collection.FindOneByIdAsync(evt.AggregateId).ConfigureAwait(false);
            Assert.That(rm.ProjectedPosition, Is.EqualTo(notHandledChangeset.GetChunkPosition()));
            Assert.That(rm.AggregateVersion, Is.EqualTo(notHandledChangeset.AggregateVersion));
            Assert.That(rm.LastProcessedVersions, Is.EquivalentTo(new[] { 1L, 2L, 3L, 4L }));
        }
    }
}
