using Jarvis.Framework.Shared.IdentitySupport;
using NUnit.Framework;
using System;
using System.Linq;
using System.Threading;

namespace Jarvis.Framework.Tests.SharedTests.IdentitySupport
{
    /// <summary>
    /// Tag cache of <see cref="EventStoreIdentity"/>. The cache is static, so every test uses
    /// identity types generated on the fly: a type is guaranteed to be used for the first time.
    /// </summary>
    [TestFixture]
    public class EventStoreIdentityTagCacheTests
    {
        private static Type NewIdentityType(string tag)
        {
            var typeName = tag + "Id";
            return IdentityManagerRegistrationTests
                .BuildIdentityAssembly(typeName)
                .GetType(typeName, throwOnError: true);
        }

        [Test]
        public void First_use_publishes_tag_and_reverse_mapping_with_same_instance()
        {
            var tag = "TagCacheFirstUse" + Guid.NewGuid().ToString("N");
            var type = NewIdentityType(tag);

            var fromType = EventStoreIdentity.GetTagForIdentityClass(type);

            Assert.That(fromType, Is.EqualTo(tag));
            Assert.That(EventStoreIdentity.TryGetTag(tag.ToUpperInvariant() + "_42", out var fromString), Is.True);
            Assert.That(fromString, Is.SameAs(fromType));
            Assert.That(EventStoreIdentity.GetTagForIdentityClass(type), Is.SameAs(fromType));
        }

        [Test]
        public void Concurrent_first_use_returns_one_instance_and_reverse_mapping_is_always_visible()
        {
            const int typeCount = 50;
            var threadCount = Math.Max(4, Environment.ProcessorCount);

            for (int i = 0; i < typeCount; i++)
            {
                var tag = "TagCacheRace" + i + "x" + Guid.NewGuid().ToString("N");
                var type = NewIdentityType(tag);
                var id = tag + "_1";

                using var barrier = new Barrier(threadCount);
                var results = new (string tag, bool resolved, string reverse)[threadCount];

                // Dedicated threads: the barrier needs all of them running at the same time.
                var threads = Enumerable.Range(0, threadCount).Select(t => new Thread(() =>
                {
                    barrier.SignalAndWait();
                    var fromType = EventStoreIdentity.GetTagForIdentityClass(type);
                    // As soon as a thread has the tag, the reverse mapping must resolve it.
                    var resolved = EventStoreIdentity.TryGetTag(id, out var reverse);
                    results[t] = (fromType, resolved, reverse);
                })).ToArray();
                foreach (var thread in threads) thread.Start();
                foreach (var thread in threads) Assert.That(thread.Join(TimeSpan.FromSeconds(10)), Is.True, "thread did not complete");

                var first = results[0].tag;
                Assert.That(results.All(r => r.resolved), Is.True, "TryGetTag failed right after GetTagForIdentityClass returned");
                Assert.That(results.All(r => ReferenceEquals(r.tag, first)), Is.True, "Racing threads received different tag instances");
                Assert.That(results.All(r => ReferenceEquals(r.reverse, first)), Is.True, "Reverse mapping holds a different tag instance");
                Assert.That(EventStoreIdentity.GetTagForIdentityClass(type), Is.SameAs(first));
            }
        }

        [Test]
        public void Classes_whose_names_differ_only_in_casing_keep_their_own_tag()
        {
            var suffix = Guid.NewGuid().ToString("N");
            var first = NewIdentityType("CaseTag" + suffix);
            var second = NewIdentityType("CASETAG" + suffix.ToUpperInvariant());

            var firstTag = EventStoreIdentity.GetTagForIdentityClass(first);
            var secondTag = EventStoreIdentity.GetTagForIdentityClass(second);

            Assert.That(firstTag, Is.EqualTo("CaseTag" + suffix));
            Assert.That(secondTag, Is.EqualTo("CASETAG" + suffix.ToUpperInvariant()));
            // The reverse map is case insensitive, the first registered casing owns it.
            Assert.That(EventStoreIdentity.TryGetTag(secondTag + "_1", out var reverse), Is.True);
            Assert.That(reverse, Is.SameAs(firstTag));
        }
    }
}
