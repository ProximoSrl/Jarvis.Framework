# AtomicMongoCollectionWrapper\<TModel\> - How It Works

## Overview

`AtomicMongoCollectionWrapper<TModel>` is the MongoDB-backed implementation of `IAtomicCollectionWrapper<TModel>`. It manages persistence, idempotency, and version tracking for **atomic readmodels** - readmodels that are rebuilt from a single aggregate's event stream.

Each wrapper instance is bound to **one** readmodel type `TModel` and therefore to **one** MongoDB collection (named by convention via `CollectionNames.GetCollectionName<TModel>()`).

## Class Hierarchy

```
IAtomicCollectionReaderQueryable<TModel>   (AsQueryable, AsQueryableSecondaryPreferred)
  |
IAtomicCollectionReader<TModel>            (FindOneByIdAsync, FindOneByIdAndCatchupAsync, FindManyAsync, FindOneByIdAtCheckpointAsync, FindManyAndCatchupAsync, FindManyByIdListAndCatchupAsync)
  |
IAtomicCollectionWrapper<TModel>           (UpsertAsync, UpsertForceAsync, UpsertBatchAsync, UpdateAsync, UpdateVersionAsync, UpdateVersionBatchAsync, DeferUpdateVersionAsync, FlushDeferredUpdatesAsync, DeleteAsync)
  |
AtomicMongoCollectionWrapper<TModel>       (concrete implementation)
```

## Construction & Initialization

On construction, the wrapper:

1. **Resolves the MongoDB collection** using `CollectionNames.GetCollectionName<TModel>()` (strips "ReadModel" suffix from type name).
2. **Creates a secondary-preferred collection reference** for read-replica queries.
3. **Manages indexes** - drops obsolete indexes (`ReadModelVersion`, `ProjectedPosition`, `ReadModelVersionForFixer`) and ensures the current composite index `ReadModelVersionForFixer2` exists (on `ProjectedPosition + ReadModelVersion + Id`).
4. **Reads the current readmodel version** from `IAtomicReadModelFactory.GetReamdodelVersion(typeof(TModel))`.

## Key Concepts

### Idempotency Model

Every atomic readmodel carries two version numbers:

- **`AggregateVersion`** (Int64): the stream position of the last event processed. Grows monotonically per aggregate.
- **`ReadModelVersion`** (Int32): a code-level version that changes when the readmodel's projection logic changes (schema migration).

**Write rules (with version check enabled):**
- If the database already holds a record whose `AggregateVersion` is **greater** than the incoming model, the write is **skipped**.
- If `AggregateVersion` is **equal** but `ReadModelVersion` on disk is **>=** the incoming value, the write is also **skipped**.
- Otherwise the incoming model wins.

This means the system is safe under concurrent writes: two threads projecting the same aggregate will converge to the highest version.

### ModifiedWithExtraStreamEvents Guard

If `model.ModifiedWithExtraStreamEvents == true`, all write operations (Upsert, Update, UpdateVersion) are **silently skipped**. This prevents persisting a readmodel that was temporarily modified with events from a draft or external stream.

### Faulted Readmodel Recovery

When `FindOneByIdAsync` detects a faulted readmodel (with `FaultRetryCount < 3`), it attempts a **live re-projection** via `ILiveAtomicReadModelProcessor.ProcessAsync`. If re-projection succeeds, the fixed readmodel is saved. If it fails again, the `FaultRetryCount` is incremented to eventually stop retrying.

### Live Catchup

`FindOneByIdAndCatchupAsync` loads the readmodel from storage, then calls `ILiveAtomicReadModelProcessor.CatchupAsync` to replay any events that arrived after the last persisted version. This gives callers a **fully up-to-date** in-memory readmodel without waiting for the projection engine to catch up.

## Write Operations

### Single-Record Operations

| Method | Behavior |
|---|---|
| `UpsertAsync` | Read existing version from DB (projected fields only). Skip if DB version is newer. Try insert if no record exists; fall back to `UpdateAsync` on duplicate key. |
| `UpsertForceAsync` | Same as `UpsertAsync` but **skips the AggregateVersion check** - always overwrites. |
| `UpdateAsync` | `FindOneAndReplace` with a filter that enforces `Id` match + version is lower or equal. Does nothing if the filter doesn't match (idempotent). |
| `UpdateVersionAsync` | Partial update (`$set`) of only `ProjectedPosition`, `AggregateVersion`, and `LastProcessedVersions`. Used when the readmodel was **not modified** by the events in a changeset but we still want to track that those events were processed. Falls back to `UpsertAsync` if the record doesn't exist yet. |
| `DeleteAsync` | Simple `DeleteOne` by Id. |

### Batch Operations

| Method | Behavior |
|---|---|
| `UpsertBatchAsync` | Validates IDs (no nulls, no duplicates). Fetches existing versions in bulk. Builds a `BulkWrite` with `ReplaceOneModel` (with upsert for new records, with version filter for existing). Executes via `BatchWriteHelper.ExecuteInChunksAsync` for optional parallel chunking. |
| `UpdateVersionBatchAsync` | Same validation. Queries which IDs already exist. For existing records: builds `UpdateOneModel` operations (partial `$set` of version fields with `AggregateVersion < model.AggregateVersion` filter). For missing records: uses `InsertMany`. Both executed via `BatchWriteHelper`. |

### BatchWriteHelper

`BatchWriteHelper.ExecuteInChunksAsync` is a generic helper that:
- Takes a `List<T>` of operations and a `BatchWriteOptions`.
- If `DegreeOfParallelism == 1` (default): executes everything as a single call.
- If `DegreeOfParallelism > 1`: splits the list into N roughly equal chunks and executes them via `Parallel.ForEachAsync`.

All bulk writes use `IsOrdered = false` to maximize throughput (MongoDB can parallelize unordered writes internally).

## Read Operations

| Method | Behavior |
|---|---|
| `AsQueryable` / `AsQueryableSecondaryPreferred` | Returns `IQueryable<TModel>` for LINQ queries (primary or secondary preferred). |
| `FindOneByIdAsync` | Loads by Id. Auto-fixes faulted readmodels and version mismatches via live re-projection. |
| `FindOneByIdAndCatchupAsync` | Loads + catches up with latest events from NStore. Creates the readmodel on-the-fly if it doesn't exist. |
| `FindOneByIdAtCheckpointAsync` | Rebuilds the readmodel in memory up to a specific global checkpoint position. |
| `FindManyAsync` | Filter-based query with optional version fix pass. |
| `FindManyAndCatchupAsync` | Filter-based query + batch catchup via `CatchupBatchAsync`. Processes in configurable batches to limit memory. |
| `FindManyByIdListAndCatchupAsync` | Given a list of IDs, loads existing + creates missing, then batch catchup. Returns only readmodels with `AggregateVersion > 0`. |

## How the Projection Engine Uses the Wrapper

The `AtomicProjectionEngine` dispatches changesets through a TPL Dataflow pipeline. For each changeset:

1. The changeset is routed to `AtomicReadmodelProjectorHelper<TModel>` instances based on the aggregate identity type.
2. The helper calls `Handle()` which:
   - Loads the readmodel via `FindOneByIdAsync`
   - Creates a new one if not found
   - Calls `ProcessChangeset` on the readmodel
   - If modified: calls `UpsertAsync` (new) or `UpdateAsync` (existing)
   - If NOT modified: calls `UpdateVersionAsync` to track the version bump
3. The batch variant `HandleManyAsync()` loads all readmodels upfront, processes changesets in parallel, then calls `UpsertBatchAsync` to persist everything at once.

## Current Limitation: Single-Type Batching Only

**All batch operations (`UpsertBatchAsync`, `UpdateVersionBatchAsync`) operate on a single `TModel` type**, because:

1. Each `AtomicMongoCollectionWrapper<TModel>` holds a reference to `IMongoCollection<TModel>` - a specific MongoDB collection.
2. Different readmodel types live in **different MongoDB collections** (named by convention).
3. MongoDB's `BulkWrite` API operates on a single collection.

This means that when the projection engine processes a changeset that touches N different readmodel types, it currently makes N separate MongoDB round-trips (one per type), even though the writes could potentially be batched across types within a single database connection.
