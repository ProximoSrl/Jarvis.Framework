# Durable checkpoint journal and MongoDB flush

Live projection checkpoints are persisted in two stages:

1. The dispatch hot path records progress in a local per-slot journal and updates
   the in-memory checkpoint.
2. A timer, an explicit flush, or tracker disposal copies the latest in-memory
   value for every slot to MongoDB in one unordered bulk operation.

This design removes MongoDB latency from event dispatch while retaining local
crash recovery for commits that produced projection side effects.

## Dispatch path

`ConcurrentCheckpointTracker.UpdateSlotAndSetCheckpointAsync` does not write to
MongoDB. It calls the durable store first, then advances the in-memory value.

- If the commit dispatched at least one event, the journal write is forced to
  stable storage before the method returns. This prevents the side effects from
  being replayed after a process crash on the same host with the same journal.
- If the commit dispatched no events, the journal write is not forced to stable
  storage. Losing that progress can cause harmless redispatch after a crash.
- Journal records are monotonic during normal processing. A token less than or
  equal to the current token cannot move a slot backwards.

A journal failure is not hidden: it prevents the in-memory checkpoint from being
advanced and propagates to the caller.

## Journal format

`FileSystemCheckpointDurableStore` keeps one file per slot and caches one open
file handle per slot. Each file contains two fixed-size records. A record stores:

- a monotonically increasing sequence number;
- the checkpoint token;
- an FNV-1a checksum over the sequence number and token.

Writes alternate between the two records. On startup, the valid record with the
highest sequence number wins. If a process stops during a write, the checksum
allows a torn record to be rejected and the previous record to be recovered.

The file store assumes a single writer for each slot. Concurrent, interleaved
writes from separate store instances to the same journal file are unsupported.

## Startup reconciliation

During setup, the tracker loads checkpoints from MongoDB and reads the local
journal. For each registered projection, it starts from the greater of the
MongoDB value and the journal value.

The default journal directory is under the operating system temporary directory:

```text
<temp>/jarvis-framework-checkpoints/<database-seed>
```

The database seed is stored in the checkpoints collection so restarts using the
same database on the same host select the same directory. Different databases
receive different directories.

The default location is local recovery state, not shared durable storage. Moving
the process to another host, replacing a container, or clearing the temporary
directory can remove journal-only progress. Configure deployment storage and
process ownership with that boundary in mind.

## MongoDB flush

When automatic flushing is enabled, the constructor's positive timeout creates a
periodic timer. A non-positive timeout disables the timer; explicit and shutdown
flushes remain available.

Each flush:

1. takes the maximum in-memory checkpoint for every slot;
2. creates one monotonic MongoDB update model per slot;
3. sends all models with one unordered `BulkWriteAsync` call.

The MongoDB filter updates a slot only when its stored value is absent or lower
than the in-memory token. Repeating a flush is therefore safe. Timer, explicit,
and shutdown flushes are serialized by the tracker. Flush failures are logged;
the next flush retries from the current in-memory state.

MongoDB readers may observe an older checkpoint until a flush succeeds. The
local journal is the recovery source for newer progress on the current host.

## Rebuilds

A rebuild is allowed to lower or clear a checkpoint, so it cannot use the normal
monotonic journal operation. `RebuildStarted` serializes against MongoDB flushes,
resets the database checkpoint, adjusts the in-memory value, and calls the
journal's exact-value `Reset` operation. The reset is forced to stable storage so
a restart cannot resurrect the pre-rebuild token from the local journal.

## Observability

This checkpoint path currently publishes no `System.Diagnostics.Metrics`
instruments. Operational monitoring should use the existing logs for timed,
explicit, and shutdown flush failures. Do not rely on the counters or histograms
described by older bulk-write-batcher documentation; that batcher is no longer
part of the implementation.

## Verification focus

Changes to this path should preserve these behaviors:

- the dispatch path performs no MongoDB write;
- dispatched commits force the journal to stable storage;
- non-dispatched commits can avoid an fsync;
- startup selects the maximum MongoDB/journal token;
- torn or corrupt records fall back to the other valid record;
- normal writes are monotonic and rebuild resets can move backwards;
- all slots are included in a single unordered MongoDB bulk operation;
- explicit, timed, shutdown, and rebuild writes remain serialized;
- disposal performs a final MongoDB flush and closes journal handles.
