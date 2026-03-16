# Projection Engine Overview

Use this page before opening framework code when the task involves live
projection dispatch, rebuild behavior, slots, or checkpoints.

## Mental model

The projection engine groups projections by slot and dispatches commits through
polling clients. Slots are the unit of ordered processing, while checkpoints
track how far each projection has advanced.

The rebuild pipeline replays events up to the relevant checkpoint target and
flushes rebuilt state back to storage.

## Main concepts

- `IProjection`: the projection contract implemented by read-side handlers.
- `ProjectionEngine`: live dispatch engine for normal projection processing.
- `RebuildProjectionEngine` and `RebuildProjectionEngineV2`: rebuild pipelines
  for replaying history.
- `AtomicProjectionEngine`: projection engine specialized for atomic read
  models.
- `IConcurrentCheckpointTracker`: checkpoint source used to monitor progress
  and rebuild state.

## When to start here

- A projection is not updating or appears stuck behind a checkpoint.
- A rebuild behaves differently from live processing.
- A slot configuration or bucket assignment affects throughput or ordering.
- An atomic read model catches up incorrectly and the problem seems lower-level
  than the reader API.

## Read next in code

- `Jarvis.Framework/ProjectionEngine/ProjectionEngine.cs`
- `Jarvis.Framework/ProjectionEngine/Rebuild/RebuildProjectionEngineV2.cs`
- `Jarvis.Framework/ProjectionEngine/Atomic/AtomicProjectionEngine.cs`
- `Jarvis.Framework/Events/IProjection.cs`

## Practical rule

If the problem is about projection lifecycle or replay orchestration, inspect
projection engine code before diving into individual projection implementations.
