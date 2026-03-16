# Reading Atomic Read Models

This is the main guide for reading atomic read models in this codebase.

## Main API

`IAtomicCollectionReader<TModel>`

This is the primary reader abstraction over persisted atomic read models.

## What it provides

### Load one read model by id

```csharp
var readModel = await reader.FindOneByIdAsync(
    aggregateId.AsString(),
    cancellationToken);
```

Important behavior:

- loads from storage
- can auto-fix outdated or faulted read models
- can trigger in-memory reprojection when stored version is stale

### Load one and catch up with latest events

```csharp
var readModel = await reader.FindOneByIdAndCatchupAsync(
    aggregateId.AsString(),
    cancellationToken);
```

Use this when you want the freshest in-memory result, even if storage lags behind event processing.

### Load many by query

```csharp
var readModels = await reader.FindManyAsync(
    rm => rm.SomeProperty == value,
    fixVersion: false,
    cancellationToken);
```

`fixVersion: true` asks the reader to ensure returned read models are upgraded to the latest read-model version.

### Load one at a specific checkpoint

```csharp
var historical = await reader.FindOneByIdAtCheckpointAsync(
    aggregateId.AsString(),
    chunkPosition,
    cancellationToken);
```

This is the time-travel/historical read API. The checkpoint is the global store position, not the aggregate version.

### Load many and catch up in batch

```csharp
var readModels = await reader.FindManyAndCatchupAsync(
    rm => rm.Status == status,
    maxBatchSize: 100,
    cancellationToken);
```

### Load many by id and create missing in-memory read models if needed

```csharp
var readModels = await reader.FindManyByIdListAndCatchupAsync(
    idList,
    maxBatchSize: 100,
    cancellationToken);
```

This is useful when you have aggregate ids first and need projected read models second.

## Lower-level projection API

`ILiveAtomicReadModelProcessor`

This is the lower-level projection engine used by the reader for replay and catch-up.

## Common operations

### Project to a specific aggregate version

```csharp
var readModel = await processor.ProcessAsync<MyAtomicReadModel>(
    aggregateId.AsString(),
    versionUpTo,
    cancellationToken);
```

### Project to a global checkpoint position

```csharp
var readModel = await processor.ProcessUntilChunkPositionAsync<MyAtomicReadModel>(
    aggregateId.AsString(),
    positionUpTo,
    cancellationToken);
```

### Project to a UTC point in time

```csharp
var readModel = await processor.ProcessUntilUtcTimestampAsync<MyAtomicReadModel>(
    aggregateId.AsString(),
    utcTimestamp,
    cancellationToken);
```

### Catch up an existing in-memory read model

```csharp
await processor.CatchupAsync(readModel, cancellationToken);
```

### Catch up many read models efficiently

```csharp
await processor.CatchupBatchAsync(readModels, cancellationToken);
```

## Enhanced API

`ILiveAtomicReadModelProcessorEnhanced` adds:

- projecting one aggregate into multiple read model types in one call
- custom stop conditions while replaying

Use it when a single stream feeds multiple atomic views or when you need “replay until condition”.

## Input expectations

- `id` is the aggregate id string, usually `EventStoreIdentity.AsString()`
- version-based APIs use aggregate version, not global stream position
- time-based projection expects UTC timestamps

## LLM rules of thumb

- Prefer `IAtomicCollectionReader<TModel>` for application reads.
- Prefer processor APIs when you explicitly need replay/catch-up logic detached from storage.
- Pass canonical aggregate ids, not ad hoc strings.
- Use catch-up APIs when you already have a persisted or cached read model and only need newer events.

## Read next

- [Implementing Atomic Read Models](implementing.md)
- [Identity Overview](../identity/README.md)
