# Durable Checkpoint Bulk-Write Batching

## Why this exists

The Jarvis WorkTask massive-upsert control at production commit `43b115007c`
(`2026-07-11-fresh-atomic-storage-load-results.md`) ran on a fresh, CPU-capped
local environment with `orleansEngineToTheMax` and the pure Orleans classic
projection engine enabled. Its 6,840 tracked WorkTasks produced about 24,600
handled checkpoint calls. That number is a logical tracker-call count, not a
claim that MongoDB acknowledged exactly 24,600 wire commands: a checkpoint call
creates one write model, while driver command telemetry is the authority for
server round trips. The same hot path is more expensive on Atlas Flex because
each independently awaited write also pays remote latency and connection
checkout overhead. The optimization target is database round trips, not
CPU-only work.

`ConcurrentCheckpointTracker` now gives concurrently ready, distinct slot heads
a one-millisecond collection window and submits them as one unordered MongoDB
`BulkWrite`. More than one checkpoint for the same slot is deliberately rejected
from a single bulk: same-slot requests remain FIFO heads and the next head cannot
execute until the previous write has an acknowledgement or failure.

The one-millisecond default is intentionally a bounded latency/throughput
tradeoff: it gives concurrently completing Orleans slots one scheduler turn to
join the batch while capping the normal extra durability window at a low
single-digit duration. It is not a universal optimum. Retain or tune it only
after the batch-size and enqueue-to-acknowledgement histograms show that the
round-trip reduction pays for the added queueing at production concurrency.

### Configuration and tuning

The production `ConcurrentCheckpointTracker` constructor currently fixes the
collection window at `TimeSpan.FromMilliseconds(1)`. The Framework has an
internal constructor that accepts a custom `TimeSpan`, primarily for tests and
in-assembly integration, but the value is not currently exposed as a host
application setting. A zero window removes the intentional collection wait;
negative values are invalid.

Increasing the window can improve throughput only when multiple distinct slots
become ready during the longer interval: the batch can grow, reducing bulk
calls and MongoDB round trips. It also increases the time a caller waits for a
durable acknowledgement and extends the interval in which a crash can cause
at-least-once replay. It does not merge same-slot requests, which remain FIFO.
Decreasing the window has the opposite tradeoff: lower acknowledgement latency
and replay exposure, but generally smaller batches and more MongoDB calls.

The default should therefore not be widened based on intuition alone. Compare
at least `0 ms`, `1 ms`, `2 ms`, and `5 ms` under production-like concurrency,
and use the batch-size, Mongo-bulk-call, enqueue-to-acknowledgement, and
Mongo-duration metrics together with p95/p99 latency and replay/error checks.
Keep the smallest window that produces a material round-trip reduction without
an unacceptable acknowledgement-latency increase.

Same-slot requests are not collapsed to their maximum checkpoint while the
window is open. Collapsing them would delay the existing per-chunk durability
boundary and therefore delay `SetDispatched`, enlarging crash replay and
duplicate side-effect or command exposure. Sending multiple same-slot models in
one unordered bulk would also allow the later token to execute before the
earlier token. Cross-slot heads have no such ordering dependency, so they are
the safe batching unit.

## Durability and ordering invariants

- `projection.CheckpointProjected(...)` keeps its legacy position before the
  durable tracker call.
- `UpdateSlotAndSetCheckpointAsync` completes only after that request has an
  acknowledged durable result. `SetDispatched(...)` therefore remains after the
  MongoDB acknowledgement.
- Every update uses a monotonic filter. Neither the persisted nor in-memory
  checkpoint can move backwards when an older request arrives late.
- At-least-once replay semantics are unchanged. The temporal interval between
  projection side effects and their durable checkpoint grows by up to the
  one-millisecond collection window, plus any wait behind an earlier write for
  the same slot.

## Accepted tradeoffs

The coordinator consciously accepts two regressions compared with the
pre-batching write path:

- **Head-of-line blocking:** the single runner means one stalled MongoDB bulk
  delays checkpoint durability for every slot in the batch, whereas the old
  path stalled only the affected slot.
- **Wider retry scope for ambiguous bulk failures:** a write-concern error,
  unacknowledged result, or unprocessed request fails every slot in the batch
  and re-dispatches their chunks. The retries remain at-least-once safe, but
  the old path confined that ambiguity to one slot.

## Failure, cancellation, and lifecycle behavior

The bulk is unordered so an explicit indexed write error can fail only its own
caller when MongoDB proves that every other request was processed and
acknowledged. Write-concern errors, network failures, unprocessed requests,
invalid indexes, and any ambiguous acknowledgement fail every caller in that
bulk. A monotonic retry is safe even if an ambiguous operation reached MongoDB.

The durable operation uses `CancellationToken.None`: canceling a caller's wait
does not cancel a queued write after projection side effects have occurred.
Explicit checkpoint flush first fences existing durable writes, then batches and
drains deferred checkpoints. Rebuild-final writes already await the same tracker
fence. The coordinator persists every batch through the asynchronous `BulkWriteAsync`
API. `FlushCheckpointAsync` keeps its legacy external contract: it logs and swallows
an explicit-flush persistence failure after the fence rather than failing its
caller. Normal dispatch callers still observe their durable-write failure.

## Low-cardinality telemetry

Meter `Jarvis.Framework.ProjectionEngine` publishes:

- submitted request, failed request, and logical bulk-call counters;
- actual batch-size histogram;
- enqueue-to-acknowledgement and Mongo-call duration histograms;
- actual flush counter tagged only with `reason=window|explicit`.

The bulk-call counter is a logical driver call. MongoDB can split an oversized
bulk internally, so driver command telemetry remains the authority for exact
wire round trips. Host applications should register
`ProjectionEngineTelemetry.MeterName` with their OpenTelemetry meter provider.

## MongoDB driver and server compatibility

No test suite can promise compatibility with an arbitrary future driver or
server. The enforceable guarantee is that the batching code depends only on
public driver contracts, treats every unknown result conservatively, and that a
declared compatibility matrix must pass before an upgrade is accepted.

Selective-success classification reads only the driver's public result,
write-error index, write-concern, and unprocessed-request properties, then maps
them into an internal primitive contract. Any missing acknowledgement, new
unprocessed shape, invalid/duplicate index, or otherwise unknown failure remains
ambiguous and fails the whole logical batch. A future driver change therefore
either fails at compile/test time or takes the conservative retry path; it must
never silently acknowledge an uncertain checkpoint.

The focused suite uses a real MongoDB validator to produce an unordered indexed
write error end to end. Primitive contract tests exercise acknowledgement,
request-count, indexed-error, write-concern, and unprocessed-request ambiguity
without constructing non-public driver types.

The required upgrade matrix is:

- on every framework PR, run the pinned driver against the oldest and newest
  supported MongoDB server on standalone and single-node replica-set topologies;
- on a driver-upgrade PR, run both the current and candidate driver across the
  compatibility intersection of the same server/topology matrix;
- nightly, cover every supported server major and inject write-concern and
  connection-loss ambiguity on a replica set;
- before release, run the candidate driver in a fresh Atlas Flex environment.

Each job must record the driver version, server version, feature compatibility
version, topology, container image digest, and focused-test result as artifacts.
The supported MongoDB server floor must be explicit: without it, "oldest
supported" is not a testable contract. Durable outcomes and emitted command
counts are stable assertions; complete raw BSON or driver exception formatting
are not.

## Verification and benchmark gate

Focused tests cover five distinct slots in one bulk, same-slot FIFO/separate
bulks, no acknowledgement before Mongo completion, monotonic out-of-order input,
an actual MongoDB indexed write error where only the rejected slot fails,
contract-level write-concern and unprocessed-request ambiguity where selective
success is forbidden,
explicit drain/flush behavior, and cancellation.

Before release, compare a fresh candidate environment with a fresh control using
the same Atlas tier, data, flags, warm-up, and workload. Accept only when MongoDB
command/check-out telemetry shows a material reduction and all checkpoint,
read-model, and domain-error gates remain exact. Treat end-to-end movement within
normal run variance as inconclusive. Revert if round trips do not fall, any
checkpoint diverges, or latency/CPU/memory regresses beyond the agreed benchmark
tolerance.

The first counterbalanced local evidence used Jarvis commit `70fcc88b4d`,
Framework `7.15.3` as control, Framework commit `6736146` as candidate, two CPUs,
fresh MongoDB and Elasticsearch data, `orleansEngineToTheMax=true`, and the pure
Orleans classic projection engine enabled before first boot. Across two
candidate runs versus the exact control:

- measured-ramp MongoDB commands fell from 75,460 to an average of 58,794.5
  (`-22.09%`);
- fixed-60-second MongoDB commands fell from 133,707 to 94,957 (`-28.98%`);
- average CPU time, allocations, GC pause, and peak working set improved by
  `7.75%`, `7.07%`, `9.16%`, and `6.67%` respectively;
- average end-to-end wall time moved from 16,361.819 ms to 16,599.139 ms
  (`+1.45%`, inside the five-percent variance gate); the counterbalanced repeat
  itself was `-2.53%` versus control;
- all 11 measured calls, 6,840 tracked commands, 2,000 WorkTask version/tuple/
  due-date/fault checks, and 380 pre-existing property-payload hash/version
  checks passed, with no checkpoint failure, Error/Fatal log, MongoDB wait-queue
  entry, or collector rejection in the measured window.

This local result is evidence for the round-trip mechanism, not a replacement
for the Atlas Flex release gate.

The subsequent Atlas Flex gate used MongoDB 8.0.27, MongoDB.Driver 3.8.1 in the
Jarvis Host, two effective CPUs, local Elasticsearch, and fresh logical database
prefixes. The exact Jarvis `70fcc88b4d` / Framework `7.15.3` Debug control was
compared with the same Jarvis commit consuming the Debug package built from the
hardened Framework head `a30245c`.

The official candidate reduced measured-ramp Mongo commands from 95,666 to
86,798 (`-9.2698%`), checkouts from 95,671 to 86,810 (`-9.2619%`), and
read-model update commands from 12,376 to 5,277 (`-57.3610%`). Its 9,616
checkpoint requests became 4,355 bulk calls with an average actual batch size
of 2.207 and zero failed requests. End-to-end wall time improved from
417,451.163 ms to 410,807.922 ms (`-1.5914%`), inside the five-percent timing
gate. Allocations fell 5.3917%, while CPU increased 2.4203% and GC pause rose by
21.2 ms; those secondary movements remain bounded relative to the causal
round-trip reduction.

The `read-model update commands` figure is a benchmark command category: it
counts physical MongoDB update activity observed during the measured
read-model workload, not the number of read-model documents or logical
projection updates that were applied. The checkpoint path still processed all
9,616 logical requests; the reduction comes from grouping independent
checkpoint update models into 4,355 bulk calls. The read-model payload update
logic is unchanged, so `-57.3610%` must not be interpreted as 57% fewer
read-model updates being performed.

All 11 measured operations, 2,000 WorkTask state/version checks, 20 warmups,
6,840 terminal tracker messages, and 380 preserved property-payload rows passed;
the final payload total was 4,020. Checkpoint failures, collector rejected or
failed points, and fixed-window Error/Fatal logs were zero. This closes the
remote-latency release gate for the bounded cross-slot batching design.
