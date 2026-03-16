# Identity Translation

Use this when the system has an external business key that must map to an internal aggregate id.

## Core abstraction

`AbstractIdentityTranslator<TId>` stores a mapping:

- external key: usually a business identifier or alias
- aggregate id: a typed `EventStoreIdentity`

Mappings are persisted in MongoDB in a collection named:

`map_<identitytypename lowercase>`

Example for `DocumentId`:

`map_documentid`

## What the base class provides

- `Translate(key, createOnMissing: true)`: get or create an aggregate id for an external key
- `TryTranslate(key)`: read existing mapping without creating one
- `TranslateBatchAsync(keys, createOnMissing, cancellationToken)`: batch translation
- `TryTranslateBatchAsync(keys)`: batch lookup without creating
- alias support through `AddAlias`, `ReplaceAlias`, `GetAlias`, `GetAliases`, `DeleteAliases`

## Important behavior

### Keys are normalized to lowercase

The translator lowercases external keys before storing or looking them up. Treat mappings as case-insensitive.

### Missing mappings can be auto-created

If `createOnMissing` is true, the translator asks the injected `IIdentityGenerator` for a new `TId` and stores the mapping.

### Batch translation is concurrency-aware

`TranslateBatchAsync` tries bulk insert for missing keys and tolerates duplicate-key races by re-reading the final mappings from MongoDB.

### Aliases map multiple external keys to the same aggregate id

Use aliases when one aggregate must be addressable by more than one business key.

## When to use this

Use an identity translator when:

- an aggregate is addressed by an external natural key
- that key can change but the aggregate id must remain stable
- multiple aliases must resolve to the same aggregate

Do not use it when you already have the canonical `EventStoreIdentity`.

## Read next

- [IdentityManager](identity-manager.md)
- [Aggregate Association](aggregate-association.md)
