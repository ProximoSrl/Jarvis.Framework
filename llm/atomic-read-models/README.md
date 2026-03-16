# Atomic Read Models Overview

If you were looking for `IAtomicReader`, use this page first.

## Main read API

The main read-side abstraction in this repository is:

- `IAtomicCollectionReader<TModel>`

This is the collection-oriented reader API for persisted atomic read models.

## Naming note

There is no interface literally named `IAtomicReader` in this repository.

Related atomic read-model APIs are:

- `IAtomicCollectionReaderQueryable<TModel>`
- `IAtomicCollectionWrapper<TModel>`
- `IAtomicReadModel`
- `AbstractAtomicReadModel`
- `ILiveAtomicReadModelProcessor`
- `ILiveAtomicReadModelProcessorEnhanced`
- `IAtomicReadModelFactory`

## Mental model

An atomic read model is a projection of exactly one aggregate stream.

Key properties tracked by the framework:

- `Id`: aggregate id as string
- `AggregateVersion`: latest aggregate version incorporated
- `ProjectedPosition`: latest global checkpoint token incorporated
- `ReadModelVersion`: projection schema/version signature
- `Faulted`: projection is broken and should not keep processing
- `LastProcessedVersions`: recent aggregate versions used to detect bad replay ordering

## Read next

- [Reading Atomic Read Models](reading.md): how `IAtomicCollectionReader<TModel>` loads, catches up, and time-travels read models.
- [Implementing Atomic Read Models](implementing.md): how a read model class handles events and what rules it must obey.
