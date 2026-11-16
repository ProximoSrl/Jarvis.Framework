using Jarvis.Framework.Shared.IdentitySupport;
using Jarvis.Framework.Tests.EngineTests;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Jarvis.Framework.Tests.SharedTests.IdentitySupport
{
    /// <summary>
    /// Allocations of the hot paths of the identity system: creation of an identity from a string
    /// and comparison of identities. Creation is compared with the allocation of a plain
    /// <c>new SampleAggregateId(id)</c>, so the test does not depend on the size of the object.
    /// </summary>
    [TestFixture]
    public class IdentityAllocationTests
    {
        private const int Iterations = 1000;

        private IdentityManager _sut;

        [SetUp]
        public void SetUp()
        {
            _sut = new IdentityManager(null);
            _sut.RegisterIdentitiesFromAssembly(typeof(SampleAggregateId).Assembly);
        }

        /// <summary>
        /// Bytes allocated by the current thread to run the action <see cref="Iterations"/> times,
        /// the action is called once before measuring to exclude jit and first call allocations.
        /// </summary>
        internal static long Measure(Action action)
        {
            action();
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < Iterations; i++)
            {
                action();
            }
            return GC.GetAllocatedBytesForCurrentThread() - before;
        }

        private static long MeasureDirectConstruction()
        {
            SampleAggregateId created = null;
            var allocated = Measure(() => created = new SampleAggregateId(42));
            Assert.That(created, Is.Not.Null);
            Assert.That(allocated, Is.GreaterThan(0), "Baseline must measure the allocation of the identity");
            return allocated;
        }

        [Test]
        public void ToIdentity_of_canonical_string_allocates_only_the_identity()
        {
            const string id = "SampleAggregate_42";
            var baseline = MeasureDirectConstruction();

            IIdentity created = null;
            var allocated = Measure(() => created = _sut.ToIdentity(id));

            Assert.That(created, Is.TypeOf<SampleAggregateId>());
            Assert.That(allocated, Is.EqualTo(baseline), "No object[], boxed id or substring must be allocated");
        }

        [Test]
        public void ToIdentity_of_canonical_string_does_not_allocate_the_string_representation()
        {
            const string id = "SampleAggregate_42";
            var baseline = MeasureDirectConstruction();

            string asString = null;
            var allocated = Measure(() => asString = _sut.ToIdentity(id).ToString());

            Assert.That(asString, Is.SameAs(id));
            Assert.That(allocated, Is.EqualTo(baseline));
        }

        [Test]
        public void Generic_ToIdentity_allocates_only_the_identity()
        {
            const string id = "SampleAggregate_42";
            var baseline = MeasureDirectConstruction();

            SampleAggregateId created = null;
            var allocated = Measure(() => created = _sut.ToIdentity<SampleAggregateId>(id));

            Assert.That(created.Id, Is.EqualTo(42));
            Assert.That(allocated, Is.EqualTo(baseline));
        }

        [Test]
        public void TryParse_allocates_only_the_identity()
        {
            const string id = "SampleAggregate_42";
            var baseline = MeasureDirectConstruction();

            SampleAggregateId created = null;
            var parsed = false;
            var allocated = Measure(() => parsed = _sut.TryParse(id, out created));

            Assert.That(parsed, Is.True);
            Assert.That(created.Id, Is.EqualTo(42));
            Assert.That(allocated, Is.EqualTo(baseline));
        }

        [Test]
        public void TryParse_of_identity_of_another_type_allocates_only_the_identity()
        {
            // the identity is created, then discarded because it is not of the requested type.
            const string id = "SampleAggregate_42";
            var baseline = MeasureDirectConstruction();

            var parsed = true;
            var allocated = Measure(() => parsed = _sut.TryParse<AllocationTestOtherId>(id, out _));

            Assert.That(parsed, Is.False);
            Assert.That(allocated, Is.EqualTo(baseline));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("SampleAggregate")]
        [TestCase("SampleAggregate_4a")]
        [TestCase("ThisTagIsNotKnown_42")]
        public void TryParse_of_invalid_identity_does_not_allocate(string id)
        {
            var parsed = true;
            var allocated = Measure(() => parsed = _sut.TryParse<SampleAggregateId>(id, out _));

            Assert.That(parsed, Is.False);
            Assert.That(allocated, Is.EqualTo(0));
        }

        [Test]
        public void TryGetTag_of_identity_manager_does_not_allocate()
        {
            string tag = null;
            var allocated = Measure(() => _sut.TryGetTag("sampleaggregate_42", out tag));

            Assert.That(tag, Is.EqualTo("SampleAggregate"));
            Assert.That(allocated, Is.EqualTo(0));
        }

        #region New identities

        // New identities are compared with hand written code that does the minimum needed to
        // return the same result (same tasks, same array): creating the identity must not add
        // an object[] nor a boxed id.

        private static IdentityManager CreateManagerWithFixedCounter(out FixedCounterService counter)
        {
            counter = new FixedCounterService();
            var manager = new IdentityManager(counter);
            manager.RegisterIdentitiesFromAssembly(typeof(SampleAggregateId).Assembly);
            return manager;
        }

        [Test]
        public void New_allocates_only_the_identity()
        {
            var sut = CreateManagerWithFixedCounter(out _);
            var baseline = MeasureDirectConstruction();

            IIdentity created = null;
            var allocated = Measure(() => created = sut.New(typeof(SampleAggregateId)));

            Assert.That(created, Is.EqualTo(new SampleAggregateId(FixedCounterService.Value)));
            Assert.That(allocated, Is.EqualTo(baseline));
        }

        [Test]
        public void Generic_New_allocates_only_the_identity()
        {
            var sut = CreateManagerWithFixedCounter(out _);
            var baseline = MeasureDirectConstruction();

            SampleAggregateId created = null;
            var allocated = Measure(() => created = sut.New<SampleAggregateId>());

            Assert.That(created.Id, Is.EqualTo(FixedCounterService.Value));
            Assert.That(allocated, Is.EqualTo(baseline));
        }

        [Test]
        public void NewAsync_allocates_only_the_identity_and_the_task()
        {
            IgnoreIfAsyncStateMachinesAreClasses();
            var sut = CreateManagerWithFixedCounter(out var counter);
            Task<IIdentity> task = null;
            var baseline = Measure(() => task = MinimalNewAsync(counter));

            var allocated = Measure(() => task = sut.NewAsync(typeof(SampleAggregateId)));

            Assert.That(task.IsCompletedSuccessfully, Is.True);
            Assert.That(task.Result, Is.EqualTo(new SampleAggregateId(FixedCounterService.Value)));
            Assert.That(baseline, Is.GreaterThan(0));
            Assert.That(allocated, Is.EqualTo(baseline));
        }

        [Test]
        public void Generic_NewAsync_allocates_only_the_identity_and_the_tasks()
        {
            IgnoreIfAsyncStateMachinesAreClasses();
            var sut = CreateManagerWithFixedCounter(out var counter);
            Task<SampleAggregateId> task = null;
            var baseline = Measure(() => task = MinimalGenericNewAsync(counter));

            var allocated = Measure(() => task = sut.NewAsync<SampleAggregateId>());

            Assert.That(task.IsCompletedSuccessfully, Is.True);
            Assert.That(task.Result.Id, Is.EqualTo(FixedCounterService.Value));
            Assert.That(baseline, Is.GreaterThan(0));
            Assert.That(allocated, Is.EqualTo(baseline));
        }

        [Test]
        public void NewManyAsync_allocates_only_identities_array_and_task()
        {
            const int count = 10;
            var sut = CreateManagerWithFixedCounter(out _);
            Task<SampleAggregateId[]> task = null;
            var baseline = Measure(() =>
            {
                var result = new SampleAggregateId[count];
                for (int i = 0; i < count; i++)
                {
                    result[i] = new SampleAggregateId(FixedCounterService.Value + i);
                }
                task = Task.FromResult(result);
            });

            var allocated = Measure(() => task = sut.NewManyAsync<SampleAggregateId>(count));

            Assert.That(task.Result, Has.Length.EqualTo(count));
            Assert.That(task.Result[count - 1].Id, Is.EqualTo(FixedCounterService.Value + count - 1));
            Assert.That(baseline, Is.GreaterThan(0));
            Assert.That(allocated, Is.EqualTo(baseline));
        }

        /// <summary>
        /// Without optimizations (debug build) the compiler generates a class for each async
        /// method, allocated on every call, with a field for each local: its size depends on how
        /// the method is written, the allocation cannot be compared with the minimal code. With
        /// optimizations it is a struct, never boxed when the awaited task is already completed.
        /// </summary>
        private static void IgnoreIfAsyncStateMachinesAreClasses()
        {
            var debuggable = typeof(IdentityManager).Assembly.GetCustomAttribute<DebuggableAttribute>();
            if (debuggable?.IsJITOptimizerDisabled == true)
            {
                Assert.Ignore("Allocations of async methods can be compared only in an optimized (release) build");
            }
        }

        private static async Task<IIdentity> MinimalNewAsync(FixedCounterService counter)
        {
            return new SampleAggregateId(await counter.GetNextAsync("SampleAggregate"));
        }

        private static async Task<SampleAggregateId> MinimalGenericNewAsync(FixedCounterService counter)
        {
            return (SampleAggregateId)await MinimalNewAsync(counter);
        }

        /// <summary>
        /// Counter that never allocates: always the same value, the same completed task and the
        /// same slot, so what is measured is only the creation of the identity.
        /// </summary>
        private sealed class FixedCounterService : IReservableCounterService
        {
            public const long Value = 100_000;

            private readonly Task<long> _valueTask = Task.FromResult(Value);
            private readonly ReservationSlot _slot = new ReservationSlot(Value, Value + 1000);

            public long GetNext(string serie) => Value;

            public Task<long> GetNextAsync(string serie, CancellationToken cancellationToken = default) => _valueTask;

            public ReservationSlot Reserve(string serie, int amount) => _slot;

            public Task ForceNextIdAsync(string serie, long nextIdToReturn, CancellationToken cancellationToken = default) => Task.CompletedTask;

            public Task<long> PeekNextAsync(string serie, CancellationToken cancellationToken = default) => _valueTask;

            public Task<bool> CheckValidityAsync(string serie, long value, CancellationToken cancellationToken = default) => Task.FromResult(true);
        }

        #endregion

        [Test]
        public void Equals_as_object_and_operators_do_not_allocate()
        {
            var first = new SampleAggregateId(42);
            object sameId = new SampleAggregateId(42);
            var otherId = new SampleAggregateId(43);

            var result = true;
            var allocated = Measure(() =>
            {
                result &= first.Equals(sameId);
                result &= object.Equals(first, sameId);
                result &= !first.Equals((object)otherId);
                result &= first != otherId;
                result &= !(first == otherId);
                result &= first.GetHashCode() == sameId.GetHashCode();
            });

            Assert.That(result, Is.True);
            Assert.That(allocated, Is.EqualTo(0));
        }

        [Test]
        public void Lookup_in_dictionary_and_set_does_not_allocate()
        {
            var dictionary = new Dictionary<SampleAggregateId, int>();
            var dictionaryByInterface = new Dictionary<IIdentity, int>();
            var set = new HashSet<SampleAggregateId>();
            for (int i = 0; i < 100; i++)
            {
                dictionary[new SampleAggregateId(i)] = i;
                dictionaryByInterface[new SampleAggregateId(i)] = i;
                set.Add(new SampleAggregateId(i));
            }
            var key = new SampleAggregateId(42);
            var missingKey = new SampleAggregateId(1000);

            var result = true;
            var allocated = Measure(() =>
            {
                result &= dictionary.TryGetValue(key, out var value) && value == 42;
                result &= dictionaryByInterface.TryGetValue(key, out value) && value == 42;
                result &= set.Contains(key);
                result &= !dictionary.ContainsKey(missingKey);
                result &= !set.Contains(missingKey);
            });

            Assert.That(result, Is.True);
            Assert.That(allocated, Is.EqualTo(0));
        }

        /// <summary>
        /// Only used as a type different from <see cref="SampleAggregateId"/>.
        /// </summary>
        private sealed class AllocationTestOtherId : EventStoreIdentity
        {
            public AllocationTestOtherId(long id) : base(id)
            {
            }
        }
    }
}
