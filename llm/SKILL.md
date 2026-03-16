---
name: jarvis-framework-llm
description: >
  Use this skill when working on Jarvis.Framework identity handling or atomic
  read models, especially questions about EventStoreIdentity, IdentityManager,
  identity translation, aggregate association, or IAtomicCollectionReader<TModel>.
  Start here before opening deeper docs so you can follow the repository's
  progressive-disclosure documentation structure.
---

# Jarvis.Framework LLM Guide

Start here when the task involves the Jarvis.Framework identity system or
atomic read models.

This folder is organized for progressive disclosure: read the smallest page
that answers the question, then follow links only when implementation detail
is needed.

## Use This Skill For

- Understanding how strongly typed ids work in Jarvis.Framework
- Finding the right identity-related type or API before editing code
- Tracing how ids are generated, parsed, normalized, translated, or mapped to
  aggregates
- Understanding or modifying atomic read-model reading and rebuild flows
- Answering questions about `EventStoreIdentity`, `IdentityManager`,
  `IdentityToAggregateManager`, or `IAtomicCollectionReader<TModel>`

## Workflow

1. Classify the request as either identity-related or atomic-read-model-related.
2. Read only the smallest linked document that matches the task.
3. Follow deeper links only when the current page does not answer the question.
4. When changing code, prefer the documented source roots listed below before
   searching the rest of the repository.

## Core Entry Points

- [Identity Overview](identity/README.md): start here for strongly typed ids,
  generation, parsing, normalization, translation, and aggregate mapping.
- [Atomic Read Models Overview](atomic-read-models/README.md): start here for
  aggregate-scoped read models and `IAtomicCollectionReader<TModel>`.

## Current Coverage

The first version of this skill covers:

- `EventStoreIdentity`
- `IdentityManager`
- identity translation and aggregate association
- atomic read-model reading APIs

## Source Roots

Search these areas first when you need implementation details:

- `Jarvis.Framework.Shared/IdentitySupport`
- `Jarvis.Framework.Shared/ReadModel/Atomic`
- `Jarvis.Framework/ProjectionEngine/Atomic`
- `Jarvis.Framework/Support`

## Selection Guide

- If the task is about creating, parsing, or validating aggregate ids, start
  with [identity/README.md](identity/README.md).
- If the task mentions `IAtomicReader`, `IAtomicCollectionReader<TModel>`,
  projection catch-up, replay, or time-travel reads, start with
  [atomic-read-models/README.md](atomic-read-models/README.md).
- If the user asks for implementation changes, read the overview page first,
  then inspect the relevant source root and only open deeper docs as needed.
