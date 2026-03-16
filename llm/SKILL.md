---
name: jarvis-framework-llm
description: >
  Use this skill when working on Jarvis.Framework identity handling, Mongo
  serialization and message registration, projection engine behavior, or
  atomic read models. Especially useful for questions about EventStoreIdentity,
  IdentityManager, MongoFlatMapper, MessagesRegistration, IProjection,
  ProjectionEngine, AtomicProjectionEngine, identity translation, aggregate
  association, or IAtomicCollectionReader<TModel>. Start here before opening
  deeper docs so you can follow the repository's progressive-disclosure
  documentation structure.
---

# Jarvis.Framework LLM Guide

Start here when the task involves the Jarvis.Framework identity system, Mongo
serialization and message registration, projection engines, or atomic read
models.

This folder is organized for progressive disclosure: read the smallest page
that answers the question, then follow links only when implementation detail
is needed.

## Use This Skill For

- Understanding how strongly typed ids work in Jarvis.Framework
- Finding the right identity-related type or API before editing code
- Tracing how ids are generated, parsed, normalized, translated, or mapped to
  aggregates
- Debugging Mongo persistence issues caused by flat id serialization or
  discriminator/message registration rules
- Understanding live projection dispatch, rebuild flow, checkpoints, slots,
  and atomic projection processing
- Understanding or modifying atomic read-model reading and rebuild flows
- Answering questions about `EventStoreIdentity`, `IdentityManager`,
  `IdentityToAggregateManager`, `MongoFlatMapper`,
  `MessagesRegistration`, `IProjection`, `ProjectionEngine`,
  `AtomicProjectionEngine`, or `IAtomicCollectionReader<TModel>`

## Workflow

1. Classify the request as identity-related, serialization-related,
   projection-engine-related, or atomic-read-model-related.
2. Read only the smallest linked document that matches the task.
3. Follow deeper links only when the current page does not answer the question.
4. When changing code, prefer the documented source roots listed below before
   searching the rest of the repository.

## Core Entry Points

- [Identity Overview](identity/README.md): start here for strongly typed ids,
  generation, parsing, normalization, translation, and aggregate mapping.
- [Mongo Serialization Overview](mongo-serialization/README.md): start here for
  `MongoFlatMapper`, discriminator alias mapping, and
  `MessagesRegistration.RegisterAssembly(...)` ordering rules.
- [Projection Engine Overview](projection-engine/README.md): start here for
  live projection dispatch, rebuild flow, slots, checkpoints, and
  `AtomicProjectionEngine`.
- [Atomic Read Models Overview](atomic-read-models/README.md): start here for
  aggregate-scoped read models and `IAtomicCollectionReader<TModel>`.

## Current Coverage

The first version of this skill covers:

- `EventStoreIdentity`
- `IdentityManager`
- identity translation and aggregate association
- Mongo flat mapping and message registration/discriminator rules
- `ProjectionEngine`, rebuild flow, checkpoints, slots, and
  `AtomicProjectionEngine`
- atomic read-model reading APIs

## Source Roots

Search these areas first when you need implementation details:

- `Jarvis.Framework.Shared/IdentitySupport`
- `Jarvis.Framework/Support`
- `Jarvis.Framework/ProjectionEngine`
- `Jarvis.Framework.Shared/ReadModel/Atomic`
- `Jarvis.Framework/ProjectionEngine/Atomic`
- `Jarvis.Framework/Events`

## Selection Guide

- If the task is about creating, parsing, or validating aggregate ids, start
  with [identity/README.md](identity/README.md).
- If the task involves Mongo documents storing ids as nested objects, unknown
  discriminators, `MongoFlatMapper`, `AliasClassMap`, or
  `MessagesRegistration.RegisterAssembly(...)`, start with
  [mongo-serialization/README.md](mongo-serialization/README.md), then
  inspect `Jarvis.Framework.Shared/IdentitySupport/MongoFlatMapper.cs` and
  `Jarvis.Framework/Support/MongoRegistration.cs`.
- If the task involves projection dispatch, rebuild behavior, slots,
  checkpoints, `IProjection`, `ProjectionEngine`, or
  `RebuildProjectionEngine`, start with
  [projection-engine/README.md](projection-engine/README.md).
- If the task mentions `IAtomicReader`, `IAtomicCollectionReader<TModel>`,
  projection catch-up, replay, or time-travel reads, start with
  [atomic-read-models/README.md](atomic-read-models/README.md).
- If the user asks for implementation changes, read the overview page first,
  then inspect the relevant source root and only open deeper docs as needed.
