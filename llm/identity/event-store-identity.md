# EventStoreIdentity

This is the main aggregate id base class in Jarvis.Framework.

## What it does

`EventStoreIdentity` is a strongly typed `long`-backed identity with a canonical string form:

`<Tag>_<Number>`

Examples:

- `Document_42`
- `SampleAggregate_7`

The tag is derived from the class name by removing the `Id` suffix. A class named `DocumentId` gets the tag `Document`.

## Required conventions

- Identity classes must inherit from `EventStoreIdentity`.
- Identity class names must end with `Id`.
- Concrete identity classes are expected to expose a `long` constructor for generation and usually a `string` constructor for parsing.

Typical pattern:

```csharp
public class DocumentId : EventStoreIdentity
{
    public DocumentId(long id) : base(id) { }
    public DocumentId(string id) : base(id) { }
}
```

## Parsing and validation rules

Accepted:

- `SampleAggregate_42`
- `sampleaggregate_42` for matching/parsing; tag comparison is case-insensitive
- numeric-only strings like `42` when constructing a typed instance directly

Rejected:

- missing numeric part: `SampleAggregate_`
- wrong separator: `SampleAggregate-42`
- wrong tag for the concrete type: `Document_42` passed to `SampleAggregateId`
- negative values
- non-numeric suffixes

Important nuance:

- `EventStoreIdentity.Match<T>(string)` only checks format and digits after the separator. It does not detect `long` overflow.
- Constructing the actual identity does validate numeric parsing and throws on overflow.

## Canonical form and casing

`ToString()` and `AsString()` always emit the canonical form using the type’s correct tag casing plus the parsed numeric value:

- input `sampleaggregate_007`
- canonical output `SampleAggregate_7`

`Normalize(string id)` is lighter-weight:

- fixes only the tag casing if the identity type is known
- preserves the numeric substring exactly
- returns `null` for unknown tags or numeric-only strings

That means:

- `Normalize("sampleaggregate_00042")` -> `SampleAggregate_00042`
- `new SampleAggregateId("sampleaggregate_00042").AsString()` -> `SampleAggregate_42`

## Equality model

Equality is based on identity type and canonical value, not input casing.

- `new SampleAggregateId("SampleAggregate_42")`
- `new SampleAggregateId("sampleaggregate_42")`

These compare equal and behave correctly as dictionary keys.

## When to use it

Use `EventStoreIdentity` for aggregate ids and anything that needs:

- stable typed ids
- a readable external string representation
- integration with `IdentityManager`
- event stream identity compatibility

## Read next

- [IdentityManager](identity-manager.md)
- [Aggregate Association](aggregate-association.md)
