# Implementing Atomic Read Models

Use this page when you need to create or reason about a concrete atomic read model type.

## Base type

Most implementations inherit from `AbstractAtomicReadModel`.

Typical shape:

```csharp
[AtomicReadmodelInfo("MyReadModel", typeof(MyAggregateId))]
public class MyReadModel : AbstractAtomicReadModel
{
    public MyReadModel(string id) : base(id) { }

    private void On(MyAggregateCreated evt) { }
    private void On(MyAggregateUpdated evt) { }

    protected override int GetVersion() => 1;
}
```

## How dispatch works

- event handlers are convention-based `On(<EventType>)` methods
- handlers can be private
- only events from the same aggregate id may be applied
- `ProcessChangeset` updates metadata even when the read model does not handle any event in the changeset

## Important invariants

### One stream only

Atomic read models are stream-scoped. Applying a changeset from a different aggregate id throws.

### Idempotency

If the same changeset is reprocessed after the model already reached that aggregate version, it is ignored.

### Holes are tolerated, but bad reorder is not

The implementation can tolerate missing versions temporarily, but if an older unprocessed version arrives later and is still within the retained version window, it throws and the model should be considered invalid.

### Fault state stops normal processing

Once faulted, the read model should not continue normal projection.

### `ReadModelVersion` is a schema signature

Change `GetVersion()` when the read model’s persisted shape or semantics change.

## Supporting pieces

- `IAtomicReadModelFactory`: creates instances by `(Type, string id)`
- `IAtomicReadModelInitializer`: startup hook for read-model-specific initialization

## LLM rules of thumb

- Do not add public event-dispatch methods; use `On(EventType)` handlers and let the base class dispatch.
- Keep the constructor `string id` available so the factory can instantiate the type.
- Treat `GetVersion()` as part of persistence compatibility, not as a business counter.
