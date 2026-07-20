using System;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Jarvis.Framework.Kernel.ProjectionEngine.Client
{
    /// <summary>
    /// <para>
    /// A local, crash-safe medium that stores the last dispatched checkpoint token for each slot.
    /// It is the durability substitute for the (now deferred) MongoDB write: the checkpoint tracker
    /// records every dispatched checkpoint here immediately and pushes to MongoDB only periodically.
    /// On startup the tracker reconciles the value found in MongoDB with the value found here,
    /// keeping whichever is greater, so a crash between two MongoDB flushes never causes an
    /// already-dispatched side effect to be replayed.
    /// </para>
    /// <para>
    /// Each slot gets its own small file that is opened once and kept open, and its current value
    /// is cached in memory, so a write is a single seek + 24-byte write with no reopen and no
    /// re-read. Torn-write safety is obtained with a two-record "ping-pong" layout. The exact
    /// on-disk organization is documented in the ON-DISK SCHEMA block comment at the top of the
    /// class body, so it can be understood without reading the implementation.
    /// </para>
    /// </summary>
    internal sealed class FileSystemCheckpointDurableStore : ICheckpointDurableStore
    {
        /*
         * ==================================== ON-DISK SCHEMA ====================================
         *
         * DIRECTORY  (one file per slot, opened once and kept open; slots never share a handle)
         * ---------------------------------------------------------------------------------------
         *     <storeDirectory>/
         *         <sanitized-slot-name>.<sha1(slot) first 4 bytes as 8 hex chars>.chk
         *
         *     example:  slot "default"  ->  "default.1f3a9c2b.chk"
         *
         *     The file name is the slot name with invalid path characters replaced by '_'
         *     (prefix truncated to 64 chars) followed by a stable SHA1 suffix, so two different
         *     slot names can never collide on the same file. The slot name itself is NOT parsed
         *     back from the file: every operation is addressed by the slot name the caller passes.
         *
         * FILE  (fixed 48 bytes = two 24-byte records, "A" then "B")
         * ---------------------------------------------------------------------------------------
         *     byte:  0         8         16        24        32        40        48
         *            |----------------- record A -----------------|
         *            |  seq   |  token  |  csum   |  seq   |  token  |  csum   |
         *            | Int64  |  Int64  | UInt64  | Int64  |  Int64  | UInt64  |
         *            |  (LE)  |  (LE)   |  (LE)   |  (LE)  |  (LE)   |  (LE)   |
         *            \______ record A (offset 0) ___/\______ record B (offset 24) ___/
         *
         *       seq   : write counter, starts at 1 and only increases. The record with the
         *               highest seq is the "live" (current) one.
         *       token : the persisted checkpoint value for the slot.
         *       csum  : FNV-1a 64-bit hash over the preceding 16 bytes (seq + token). A torn or
         *               never-written record fails this check and is treated as absent.
         *
         * OPEN + CACHE  (done once per slot)
         * ---------------------------------------------------------------------------------------
         *     The first time a slot is touched, its file is opened once and both records are read
         *     to seed the in-memory live state (seq, token, active record index). That handle and
         *     state are then kept in memory: Read returns the cached token and writes update the
         *     cache in place. The file is never re-read on the hot path.
         *
         * WRITE  (Record / Reset) -- the live record is never overwritten in place, no re-read
         * ---------------------------------------------------------------------------------------
         *     1. build the new record with seq = cached live seq + 1
         *     2. write it to the OTHER record slot (cached live == A -> write B, otherwise A)
         *     3. Flush(); fsync (FlushFileBuffers) only when flushToDisk == true, i.e. for a
         *        dispatched commit whose side effects must never be replayed
         *     4. update the in-memory cache (seq, token, active index)
         *
         *        live = B(200)          write 300 lands in A         outcome
         *        A[seq7,tok100,ok]  -->  A[seq9,tok300, .... ]  -->   A is now live (300)
         *        B[seq8,tok200,ok]       B[seq8,tok200, ok   ]        B is the spare for next write
         *
         *     If the process crashes during step 2, A is left torn (bad csum) and the next open
         *     falls back to B(200): the previously persisted value is never lost.
         *     - Record() is monotonic: a token <= the cached token is ignored (a bare flush is
         *       still honored so an earlier non-fsynced write reaches stable storage).
         *     - Reset() forces an exact value, bypassing the monotonic guard (used when a rebuild
         *       intentionally lowers/clears the checkpoint), and always fsyncs.
         *
         * READ  (Read / startup reconciliation -- NOT the hot path)
         * ---------------------------------------------------------------------------------------
         *     re-reads both records from disk and returns the token of the highest valid-checksum
         *     record (0 when the file is missing or holds no valid record). Read re-reads rather
         *     than trusting the cache because it is the reconciliation path and may need to observe
         *     a value written through a different handle; the hot write path stays cache-only.
         *
         * CONCURRENCY
         * ---------------------------------------------------------------------------------------
         *     A slot is written by a single dispatcher thread, so writes to one file are serialized
         *     and the in-memory cache is authoritative for that writer; the per-handle lock only
         *     guards against Dispose. Production has exactly one instance per store directory. A
         *     second instance opening the same file (as in some tests) always sees the latest value
         *     because Read re-reads from disk; only concurrent interleaved WRITES from two instances
         *     are unsupported.
         *
         * ========================================================================================
         */

        private const int RecordSize = 24;
        private const int FileSize = RecordSize * 2;

        private readonly string _directory;
        private readonly ConcurrentDictionary<string, SlotHandle> _handles =
            new ConcurrentDictionary<string, SlotHandle>(StringComparer.Ordinal);

        private volatile bool _disposed;

        public FileSystemCheckpointDurableStore(string directory)
        {
            if (String.IsNullOrWhiteSpace(directory))
            {
                throw new ArgumentException("A directory is required.", nameof(directory));
            }

            _directory = directory;
            Directory.CreateDirectory(_directory);
        }

        public long Read(string slotName)
        {
            var handle = GetHandle(slotName, createFileIfMissing: false);
            if (handle == null)
            {
                return 0L;
            }

            lock (handle.Gate)
            {
                // Read is the low-frequency reconciliation path (startup / rebuild seeding), not
                // the hot write path, so it re-reads from disk to pick up a value written through
                // a different handle (e.g. another tracker instance sharing the same file). Writes
                // stay cache-only and never re-read.
                SeedCacheFromDisk(handle);
                return handle.Token;
            }
        }

        public void Record(string slotName, long checkpointToken, bool flushToDisk)
        {
            var handle = GetHandle(slotName, createFileIfMissing: true);
            lock (handle.Gate)
            {
                if (_disposed)
                {
                    return;
                }

                if (checkpointToken <= handle.Token)
                {
                    // Monotonic: nothing new to persist. Still honor an explicit flush so
                    // any earlier non-fsynced write reaches stable storage.
                    if (flushToDisk)
                    {
                        handle.Stream.Flush(flushToDisk: true);
                    }
                    return;
                }

                WriteRecord(handle, checkpointToken, flushToDisk);
            }
        }

        public void Reset(string slotName, long checkpointToken)
        {
            var handle = GetHandle(slotName, createFileIfMissing: true);
            lock (handle.Gate)
            {
                if (_disposed)
                {
                    return;
                }

                WriteRecord(handle, checkpointToken, flushToDisk: true);
            }
        }

        private static void WriteRecord(SlotHandle handle, long checkpointToken, bool flushToDisk)
        {
            // Target the record that does not currently hold the value so a torn write cannot
            // destroy it (when neither record is valid, ActiveIndex is -1 -> target 0).
            int targetIndex = handle.ActiveIndex == 0 ? 1 : 0;
            long nextSeq = handle.Seq + 1;

            Span<byte> buffer = stackalloc byte[RecordSize];
            BinaryPrimitives.WriteInt64LittleEndian(buffer.Slice(0, 8), nextSeq);
            BinaryPrimitives.WriteInt64LittleEndian(buffer.Slice(8, 8), checkpointToken);
            BinaryPrimitives.WriteUInt64LittleEndian(buffer.Slice(16, 8), Checksum(buffer.Slice(0, 16)));

            handle.Stream.Seek(targetIndex * RecordSize, SeekOrigin.Begin);
            handle.Stream.Write(buffer);
            handle.Stream.Flush(flushToDisk);

            handle.Seq = nextSeq;
            handle.Token = checkpointToken;
            handle.ActiveIndex = targetIndex;
        }

        private SlotHandle GetHandle(string slotName, bool createFileIfMissing)
        {
            if (String.IsNullOrWhiteSpace(slotName))
            {
                throw new ArgumentException("A slot name is required.", nameof(slotName));
            }
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(FileSystemCheckpointDurableStore));
            }

            if (_handles.TryGetValue(slotName, out var existing))
            {
                return existing;
            }

            var path = GetFilePath(slotName);
            if (!createFileIfMissing && !File.Exists(path))
            {
                return null;
            }

            return _handles.GetOrAdd(slotName, _ => OpenHandle(path));
        }

        private static SlotHandle OpenHandle(string path)
        {
            // FileShare.ReadWrite so a second instance can open the same file (production has a
            // single writer; some tests open the same file from more than one instance). Each
            // instance seeds its own cache from disk at open time.
            var stream = new FileStream(
                path,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.ReadWrite,
                bufferSize: FileSize);

            if (stream.Length < FileSize)
            {
                stream.SetLength(FileSize);
            }

            var handle = new SlotHandle(stream);
            SeedCacheFromDisk(handle);
            return handle;
        }

        private static void SeedCacheFromDisk(SlotHandle handle)
        {
            Span<byte> file = stackalloc byte[FileSize];
            handle.Stream.Seek(0, SeekOrigin.Begin);
            int read = ReadFull(handle.Stream, file);

            handle.Seq = 0;
            handle.Token = 0;
            handle.ActiveIndex = -1;

            for (int index = 0; index < 2; index++)
            {
                int offset = index * RecordSize;
                if (offset + RecordSize > read)
                {
                    continue;
                }

                var record = file.Slice(offset, RecordSize);
                ulong storedChecksum = BinaryPrimitives.ReadUInt64LittleEndian(record.Slice(16, 8));
                if (storedChecksum != Checksum(record.Slice(0, 16)))
                {
                    continue; // torn / never-written record
                }

                long seq = BinaryPrimitives.ReadInt64LittleEndian(record.Slice(0, 8));
                if (handle.ActiveIndex < 0 || seq > handle.Seq)
                {
                    handle.Seq = seq;
                    handle.Token = BinaryPrimitives.ReadInt64LittleEndian(record.Slice(8, 8));
                    handle.ActiveIndex = index;
                }
            }
        }

        private static int ReadFull(Stream stream, Span<byte> buffer)
        {
            int total = 0;
            while (total < buffer.Length)
            {
                int read = stream.Read(buffer.Slice(total));
                if (read == 0)
                {
                    break;
                }
                total += read;
            }
            return total;
        }

        private static ulong Checksum(ReadOnlySpan<byte> data)
        {
            // FNV-1a 64 bit. Not cryptographic, but a torn or zeroed record is extremely
            // unlikely to match, which is all we need to reject partial writes.
            const ulong offsetBasis = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;
            ulong hash = offsetBasis;
            foreach (var b in data)
            {
                hash ^= b;
                hash *= prime;
            }
            return hash;
        }

        private string GetFilePath(string slotName)
        {
            return Path.Combine(_directory, BuildFileName(slotName));
        }

        private static string BuildFileName(string slotName)
        {
            // Keep a readable prefix but disambiguate with a stable hash so that different
            // slot names can never collide on a sanitized file name.
            var sanitized = new StringBuilder(slotName.Length);
            var invalid = Path.GetInvalidFileNameChars();
            foreach (var c in slotName)
            {
                sanitized.Append(Array.IndexOf(invalid, c) >= 0 ? '_' : c);
            }

            using var sha1 = SHA1.Create();
            var hash = sha1.ComputeHash(Encoding.UTF8.GetBytes(slotName));
            var suffix = Convert.ToHexString(hash, 0, 4).ToLowerInvariant();

            var prefix = sanitized.ToString();
            if (prefix.Length > 64)
            {
                prefix = prefix.Substring(0, 64);
            }
            return $"{prefix}.{suffix}.chk";
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;

            foreach (var handle in _handles.Values)
            {
                lock (handle.Gate)
                {
                    try
                    {
                        handle.Stream.Flush(flushToDisk: true);
                    }
                    catch
                    {
                        // best effort on shutdown
                    }
                    handle.Stream.Dispose();
                }
            }
            _handles.Clear();
        }

        private sealed class SlotHandle
        {
            public SlotHandle(FileStream stream)
            {
                Stream = stream;
            }

            public FileStream Stream { get; }

            public readonly object Gate = new object();

            /// <summary>Cached highest sequence number written so far (0 when none).</summary>
            public long Seq;

            /// <summary>Cached current checkpoint token (0 when none).</summary>
            public long Token;

            /// <summary>Index (0 or 1) of the record currently holding the value, -1 when none.</summary>
            public int ActiveIndex;
        }
    }
}
