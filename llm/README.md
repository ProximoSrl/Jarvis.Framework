# Jarvis.Framework LLM Guide

Start here. This directory is organized for progressive disclosure: read the smallest page that answers your question, then follow links only when you need implementation detail.

## Core entry points

- [Identity Overview](identity/README.md): how strongly typed ids work, how ids are generated, parsed, normalized, and mapped back to aggregates.
- [Atomic Read Models Overview](atomic-read-models/README.md): how to read or rebuild aggregate-scoped read models, starting from `IAtomicCollectionReader<TModel>`.

## Current focus

The first version of this guide covers:

- `EventStoreIdentity`
- `IdentityManager`
- identity translation and aggregate association
- atomic read model reading APIs

## Source roots

- `Jarvis.Framework.Shared/IdentitySupport`
- `Jarvis.Framework.Shared/ReadModel/Atomic`
- `Jarvis.Framework/ProjectionEngine/Atomic`
- `Jarvis.Framework/Support`
