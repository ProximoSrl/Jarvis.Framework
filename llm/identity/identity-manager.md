# IdentityManager

`IdentityManager` is the runtime registry and factory for identities.

## Primary responsibilities

- register identity types from assemblies
- generate new sequential ids
- parse strings back into typed identities
- expose tag-to-type lookup
- support reservable/offline counters

## Required setup

Before using most features, register the identity types:

```csharp
var manager = new IdentityManager(counterService);
manager.RegisterIdentitiesFromAssembly(typeof(DocumentId).Assembly);
```

Registration scans concrete `IIdentity` classes and, for `EventStoreIdentity` types, expects a constructor shaped like:

```csharp
public DocumentId(long id)
```

If that constructor is missing, registration fails.

## Common operations

### Generate a new id

```csharp
var id = manager.New<DocumentId>();
var id2 = await manager.NewAsync<DocumentId>(cancellationToken);
```

There are also non-generic overloads that take `Type`.

### Parse a string

```csharp
var identity = manager.ToIdentity("Document_42");
var typed = manager.ToIdentity<DocumentId>("Document_42");
```

Use `TryParse` when invalid input is expected:

```csharp
if (manager.TryParse<DocumentId>(value, out var id))
{
    // use id
}
```

## Important behavior

### Registration is not optional

If a tag was not registered, parsing and generation fail with a framework exception. `EventStoreIdentity` knows how to validate a concrete instance, but `IdentityManager` needs its own registry to map tags to factories.

### Tag lookup is case-insensitive

These all resolve to the same type after registration:

- `Test`
- `test`
- `TEST`
- `TestId`

Use:

```csharp
var type = manager.GetIdentityTypeByTag("Document");
```

### `ToIdentity` is stricter than direct typed construction

`IdentityManager.ToIdentity("42")` fails because it requires the `<Tag>_<Number>` format.

By contrast, `new DocumentId("42")` can succeed because `EventStoreIdentity` supports numeric-only input when the target type is already known.

### Counter services control allocation strategy

`IdentityManager` depends on a reservable counter service. In this repo that includes:

- `CounterService`: normal persisted sequence generation
- `OfflineCounterService`: reservation-based/offline generation
- `InMemoryCounterService`: lightweight/test scenarios

### Force and peek APIs exist

For operational scenarios and tests:

- `PeekNextIdAsync<T>()`
- `ForceNextIdAsync<T>(nextId)`
- `NewManyAsync<T>(count)` for batch reservation/allocation

## LLM rules of thumb

- Do not instantiate ids by concatenating strings when `IdentityManager` is available.
- Do not parse unknown id strings by splitting on `_` yourself; prefer `TryParse`, `ToIdentity`, or `GetIdentityTypeByTag`.
- When you need a specific typed id from user input, prefer `TryParse<T>` over catching exceptions.
- If parsing raw strings from external systems, remember tag matching is case-insensitive but registration is still required.

## Read next

- [EventStoreIdentity](event-store-identity.md)
- [Identity Translation](identity-translation.md)
