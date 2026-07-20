using Jarvis.Framework.Kernel.ProjectionEngine.Client;
using NUnit.Framework;
using System;
using System.IO;

namespace Jarvis.Framework.Tests.ProjectionEngineTests
{
    [TestFixture]
    public class FileSystemCheckpointDurableStoreTests
    {
        private string _directory;

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(), "jarvis-fw-durable-store-tests", Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                if (Directory.Exists(_directory))
                {
                    Directory.Delete(_directory, recursive: true);
                }
            }
            catch { /* best effort */ }
        }

        [Test]
        public void Read_returns_zero_for_unknown_slot()
        {
            using var store = new FileSystemCheckpointDurableStore(_directory);
            Assert.That(store.Read("missing"), Is.EqualTo(0));
        }

        [Test]
        public void Record_then_read_returns_value()
        {
            using var store = new FileSystemCheckpointDurableStore(_directory);
            store.Record("slot", 123, flushToDisk: true);
            Assert.That(store.Read("slot"), Is.EqualTo(123));
        }

        [Test]
        public void Record_is_monotonic()
        {
            using var store = new FileSystemCheckpointDurableStore(_directory);
            store.Record("slot", 200, flushToDisk: true);
            store.Record("slot", 100, flushToDisk: true); //lower, must be ignored
            Assert.That(store.Read("slot"), Is.EqualTo(200));
        }

        [Test]
        public void Reset_can_lower_the_value()
        {
            using var store = new FileSystemCheckpointDurableStore(_directory);
            store.Record("slot", 200, flushToDisk: true);
            store.Reset("slot", 50);
            Assert.That(store.Read("slot"), Is.EqualTo(50));
        }

        [Test]
        public void Slots_are_independent()
        {
            using var store = new FileSystemCheckpointDurableStore(_directory);
            store.Record("slot-a", 10, flushToDisk: true);
            store.Record("slot-b", 20, flushToDisk: true);
            Assert.That(store.Read("slot-a"), Is.EqualTo(10));
            Assert.That(store.Read("slot-b"), Is.EqualTo(20));
        }

        [Test]
        public void Value_survives_a_reopen_simulating_restart()
        {
            using (var store = new FileSystemCheckpointDurableStore(_directory))
            {
                store.Record("slot", 777, flushToDisk: true);
            }

            using (var reopened = new FileSystemCheckpointDurableStore(_directory))
            {
                Assert.That(reopened.Read("slot"), Is.EqualTo(777));
            }
        }

        [Test]
        public void A_torn_write_of_the_newest_record_falls_back_to_the_previous_value()
        {
            string filePath;
            using (var store = new FileSystemCheckpointDurableStore(_directory))
            {
                store.Record("slot", 100, flushToDisk: true); // record A (seq 1)
                store.Record("slot", 200, flushToDisk: true); // record B (seq 2, now the active one)
                filePath = Directory.GetFiles(_directory, "*.chk")[0];
            }

            // Simulate a crash that left the second record (offset 24..48) partially/incorrectly
            // written by corrupting its checksum. The previous, intact record must win.
            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                fs.Seek(40, SeekOrigin.Begin); // checksum of the second record
                fs.Write(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF });
                fs.Flush(true);
            }

            using (var reopened = new FileSystemCheckpointDurableStore(_directory))
            {
                Assert.That(reopened.Read("slot"), Is.EqualTo(100));
            }
        }
    }
}
