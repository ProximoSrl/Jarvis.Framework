# Identity Overview

Use this page to orient an LLM before opening the detailed identity docs.

## What identity means in this library

Jarvis.Framework uses strongly typed ids instead of raw strings. The common flow is:

1. Define an id type by inheriting from `EventStoreIdentity`.
2. Register identity types in `IdentityManager`.
3. Generate new ids through `IdentityManager.New<T>()` or `NewAsync<T>()`.
4. Parse external strings back to typed ids through `ToIdentity`, `TryParse`, or `GetIdentityTypeByTag`.
5. Optionally translate external business keys to aggregate ids through an `AbstractIdentityTranslator<TId>`.
6. Optionally map an id type back to the aggregate root type through `IdentityToAggregateManager`.

## Read next

- [EventStoreIdentity](event-store-identity.md): id format, validation rules, normalization, casing, and edge cases.
- [IdentityManager](identity-manager.md): registration, generation, parsing, tag lookup, and operational rules.
- [Identity Translation](identity-translation.md): external-key-to-id mapping and aliases.
- [Aggregate Association](aggregate-association.md): how the framework infers an aggregate type from an identity type.

## Mental model

- `IIdentity` is the minimum abstraction: it only promises `AsString()`.
- `AbstractIdentity<TKey>` supplies equality and storage for the underlying key.
- `EventStoreIdentity` is the dominant aggregate identity type in this codebase: it is `AbstractIdentity<long>` plus the `Tag_Number` wire format.

If you only need to create or parse aggregate ids, you usually need only `EventStoreIdentity` and `IdentityManager`.
