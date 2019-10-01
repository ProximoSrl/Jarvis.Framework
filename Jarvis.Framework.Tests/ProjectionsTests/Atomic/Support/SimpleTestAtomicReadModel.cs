using Jarvis.Framework.Kernel.ProjectionEngine.Atomic;
using Jarvis.Framework.Shared.Events;
using Jarvis.Framework.Shared.ReadModel.Atomic;
using Jarvis.Framework.Tests.EngineTests;
using MongoDB.Driver;
using System;
using System.Threading.Tasks;

namespace Jarvis.Framework.Tests.ProjectionsTests.Atomic.Support
{
    [AtomicReadmodelInfo("SimpleTestAtomicReadModel", typeof(SampleAggregateId))]
    public class SimpleTestAtomicReadModel : AbstractAtomicReadModel
    {
        public SimpleTestAtomicReadModel(string id) : base(id)
        {
        }

        public Int32 TouchCount { get; private set; }

        public Boolean Created { get; private set; }

        public String ExtraString { get; set; }

        protected override void BeforeEventProcessing(DomainEvent domainEvent)
        {
            ExtraString += $"B-{domainEvent.MessageId}";
            base.BeforeEventProcessing(domainEvent);
        }

        protected override void AfterEventProcessing(DomainEvent domainEvent)
        {
            ExtraString += $"A-{domainEvent.MessageId}";
            base.AfterEventProcessing(domainEvent);
        }

#pragma warning disable S1144 // Unused private types or members should be removed
#pragma warning disable S1172 // Unused method parameters should be removed
        private void On(SampleAggregateCreated evt)
        {
            ExtraString += $"IN-{evt.MessageId}";
            Created = true;
            TouchCount = 0;
        }

        private void On(SampleAggregateTouched evt)
        {
            if (TouchCount >= TouchMax)
                throw new Exception();

            TouchCount += FakeSignature;
        }
#pragma warning restore S1172 // Unused method parameters should be removed
#pragma warning restore S1144 // Unused private types or members should be removed

        protected override int GetVersion()
        {
            return FakeSignature;
        }

        public static Int32 FakeSignature { get; set; }

        public static Int32 TouchMax { get; set; } = Int32.MaxValue;
    }

    public class SimpleTestAtomicReadModelInitializer : IAtomicReadModelInitializer
    {
        private readonly IAtomicMongoCollectionWrapper<SimpleTestAtomicReadModel> _atomicCollectionWrapper;

        public SimpleTestAtomicReadModelInitializer(IAtomicMongoCollectionWrapper<SimpleTestAtomicReadModel> atomicCollectionWrapper)
        {
            _atomicCollectionWrapper = atomicCollectionWrapper;
        }

        public Boolean Initialized { get; set; }

        public Task Initialize()
        {
            Initialized = true;
            return _atomicCollectionWrapper
                .Collection
                .Indexes
                    .CreateOneAsync(new MongoDB.Driver.CreateIndexModel<SimpleTestAtomicReadModel>(
                        Builders<SimpleTestAtomicReadModel>.IndexKeys.Ascending(_ => _.TouchCount),
                        new CreateIndexOptions()
                        {
                            Name = "Test index"
                        }
                     ));
        }
    }
}
