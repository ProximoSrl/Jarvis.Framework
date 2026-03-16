# Aggregate Association

Use this when you have an `EventStoreIdentity` instance and need the aggregate root type.

## Main type

`IdentityToAggregateManager`

## What it does

It scans assemblies for aggregate roots and builds a cache from identity type to aggregate type.

This is convention-based:

- aggregate roots must inherit from `AggregateRoot<,>`
- the aggregate name is expected to match the identity tag
- namespace proximity is also used during fallback lookup

Example expectation:

- identity type `OrderId`
- identity tag `Order`
- aggregate type `Order`

## Typical usage

1. Scan assemblies once:

```csharp
var mapper = new IdentityToAggregateManager();
mapper.ScanAssemblyForAggregateRoots(typeof(Order).Assembly);
```

2. Resolve from a typed identity:

```csharp
Type aggregateType = mapper.GetAggregateFromId(orderId);
```

## Failure modes

- multiple aggregates claim the same identity type
- no aggregate matches the identity
- ambiguous convention matches in the same namespace family

If the association is important to runtime behavior, do not assume naming conventions are enough; verify the scanned assemblies actually contain the intended aggregate roots.
