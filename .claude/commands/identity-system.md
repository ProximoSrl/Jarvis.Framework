# Jarvis.Framework Identity System

You are an expert on the Jarvis.Framework identity system. Use this knowledge to answer questions, write code, and debug identity-related issues.

## Overview

The identity system provides strongly-typed, immutable identifiers for aggregates and entities. Identities follow the format `{Tag}_{NumericId}` (e.g., `Document_123`). The tag is derived from the class name by removing the `Id` suffix.

## Class Hierarchy

```
IIdentity (interface: AsString(), IEquatable<IIdentity>)
  -> AbstractIdentity<TKey> (generic base, TKey = int|long|uint|ulong|Guid|string)
       -> EventStoreIdentity (specialized: AbstractIdentity<long>)
            -> [Your concrete identity] (e.g., DocumentId, OrderId)
```

## Core Classes & Interfaces

### IIdentity
- File: `Jarvis.Framework.Shared/IdentitySupport/IIdentity.cs`
- Single method: `string AsString()`
- Implements `IEquatable<IIdentity>`

### AbstractIdentity\<TKey\>
- File: `Jarvis.Framework.Shared/IdentitySupport/AbstractIdentity.cs`
- Generic base with `Id` property (`[BsonIgnore]`, `[JsonIgnore]`)
- Abstract `GetTag()` method
- Implements equality (`==`, `!=`), `GetHashCode()`, `GetConsistentHashCode()`

### EventStoreIdentity
- File: `Jarvis.Framework.Shared/IdentitySupport/EventStoreIdentity.cs`
- Extends `AbstractIdentity<long>`
- Separator: `_` (underscore)
- Constructors: `(long id)` and `(string id)` - both validate positivity
- `FullyQualifiedId` property: `[BsonId]`, `[JsonProperty("Id")]` - the serialized form
- Static caches: `classTags` (Type -> tag), `tagToCorrectCaseMap` (case-insensitive tag lookup)

#### Key Static Methods
| Method | Description |
|--------|-------------|
| `GetTagForIdentityClass<T>()` | Returns tag for identity type (e.g., `"Document"` for `DocumentId`) |
| `Match<T>(string id)` | Validates string matches identity format (case-insensitive) |
| `Format(string tag, long value)` | Constructs identity string `"{tag}_{value}"` |
| `Normalize(string id)` | Normalizes tag casing; returns `null` if type unknown |

#### Naming Convention
- Class name **must** end with `Id`
- Tag = class name minus `Id` suffix
- Example: `SampleAggregateId` -> tag `"SampleAggregate"`

#### Validation Rules
- Numeric ID must be >= 0
- Tag must match the expected type (case-insensitive)
- Accepts plain numeric strings (e.g., `"123"`) as shorthand
- Rejects strings starting with separator

## Identity Management

### IIdentityManager = IIdentityConverter + IIdentityGenerator
- File: `Jarvis.Framework.Shared/IdentitySupport/IIdentityManager.cs`

### IdentityManager
- File: `Jarvis.Framework.Shared/IdentitySupport/IdentityManager.cs`
- Constructor takes `ICounterService`
- Must call `RegisterIdentitiesFromAssembly(Assembly)` to discover identity types
- Registration scans for non-abstract `EventStoreIdentity` subclasses with `(long)` constructor

#### Key Methods
| Method | Description |
|--------|-------------|
| `RegisterIdentitiesFromAssembly(Assembly)` | Scans and registers all identity types |
| `New<T>()` / `New(Type)` | Generate new identity (sync) |
| `NewAsync<T>()` / `NewAsync(Type)` | Generate new identity (async) |
| `NewManyAsync<T>(int count)` | Batch generate identities |
| `ToIdentity(string)` | Parse string to identity (generic) |
| `ToIdentity<T>(string)` | Parse string to typed identity |
| `TryParse<T>(string, out T)` | Safe parse without exception |
| `TryGetTag(string, out string)` | Extract tag from identity string |
| `GetIdentityTypeByTag(string)` | Lookup Type by tag |
| `ForceNextIdAsync<T>(long)` | Set next counter value |
| `PeekNextIdAsync<T>()` | Preview next ID without consuming |

## Counter Services (ID Generation Backend)

### ICounterService
- File: `Jarvis.Framework.Shared/IdentitySupport/ICounterService.cs`
- Methods: `GetNext(string)`, `GetNextAsync(string)`, `ForceNextIdAsync(string, long)`, `PeekNextAsync(string)`, `CheckValidityAsync(string, long)`

### Implementations
| Class | Collection | Use Case |
|-------|-----------|----------|
| `CounterService` | `sysCounters` | Production - MongoDB atomic `FindOneAndUpdate` with `$inc` |
| `InMemoryCounterService` | n/a | Testing - simple `Interlocked.Increment` |
| `OfflineCounterService` | `sysOfflineCounterSlots` | Offline/reserved blocks of IDs |
| `MultitenantCounterService` | delegates | Multi-tenant via `ITenantAccessor` |

### IReservableCounterService (extends ICounterService)
- `Reserve(string serie, int amount)` -> `ReservationSlot` (StartIndex, EndIndex, Size)

### IOfflineCounterService (extends IReservableCounterService)
- `AddReservation(string, ReservationSlot)`, `CountReservation(string)`, `GetReservationStarving(int, params string[])`

## Identity Translation (External Key -> Internal ID)

### AbstractIdentityTranslator\<TKey\>
- File: `Jarvis.Framework.Shared/IdentitySupport/AbstractIdentityTranslator.cs`
- Maps external string keys to `EventStoreIdentity` values
- MongoDB collection: `map_{TypeName}` (e.g., `map_testid`)
- Keys normalized to lowercase (case-insensitive)
- Nested `MappedIdentity` document: `ExternalKey` (`_id`), `AggregateId`

#### Key Methods (protected)
| Method | Description |
|--------|-------------|
| `Translate(string, bool createOnMissing)` | Sync translation, optionally auto-create |
| `TranslateBatchAsync(IEnumerable<string>, bool)` | Batch async with auto-create |
| `TryTranslate(string)` | Safe sync lookup |
| `TryTranslateBatchAsync(IEnumerable<string>)` | Batch without creating |
| `AddAlias(TKey, string)` | Add alias for identity |
| `ReplaceAlias(TKey, string)` | Replace aliases |
| `GetAlias(TKey)` / `GetAliases(TKey)` | Retrieve aliases |
| `MapIdentity(string, TKey)` | Explicit mapping |
| `DeleteAliases(TKey)` | Remove all aliases |

### SingleCollectionAbstractIdentityTranslator\<TKey\>
- File: `Jarvis.Framework.Shared/IdentitySupport/SingleCollectionAbstractIdentityTranslator.cs`
- Alternative: stores all mappings in single `mappers` collection with `IdType` field
- Useful for MongoDB Atlas free tier (500 collection limit)
- Auto-migrates from per-type collections

### IStringToIdentityAssociator\<TId\> / MongoStringToIdentityAssociator\<TId\>
- File: `Jarvis.Framework.Shared/IdentitySupport/StringToIdentityAssociator.cs`
- 1:1 string-to-ID associations with optional ID uniqueness constraint
- Methods: `Associate`, `AssociateOrUpdate`, `DeleteAssociationWithKey/Id`, `GetIdFromKey`

## Serialization

### BSON (MongoDB)
- File: `Jarvis.Framework.Shared/IdentitySupport/Serialization/EventStoreIdentityBsonSerializer.cs`
- `TypedEventStoreIdentityBsonSerializer<T>` - for specific EventStoreIdentity subtypes
- `GenericIdentityBsonSerializer` - for `IIdentity` interface
- `AbstractIdentityBsonSerializer<T, TId>` - for AbstractIdentity subtypes
- `IdentityArrayBsonSerializer<T>` - for identity arrays
- `DictionaryAsObjectJarvisSerializer<TDict, TKey, TValue>` - dictionaries with identity keys

### BSON Type Mapping
- File: `Jarvis.Framework.Shared/IdentitySupport/Serialization/EventStoreIdentityCustomBsonTypeMapper.cs`
- Maps EventStoreIdentity -> BSON string representation

### JSON (Newtonsoft.Json)
- File: `Jarvis.Framework.Shared/IdentitySupport/Serialization/EventStoreIdentityJsonConverter.cs`
- `EventStoreIdentityJsonConverter` - requires `IIdentityConverter` in constructor

### Flat Mapping Setup
- File: `Jarvis.Framework.Shared/IdentitySupport/MongoFlatMapper.cs`
- `MongoFlatMapper.EnableFlatMapping()` - registers BSON serialization providers
- `MongoFlatIdSerializerHelper` - initializes identity converter for serialization

## Aggregate Mapping

### IIdentityToAggregateManager
- File: `Jarvis.Framework.Shared/IdentitySupport/IIdentityToAggregateManager.cs`
- Maps EventStoreIdentity -> Aggregate root type

### IdentityToAggregateManager
- File: `Jarvis.Framework/Support/IdentityToAggregateManager.cs`
- `ScanAssemblyForAggregateRoots(Assembly)` discovers `AggregateRoot<TState, TId>` classes
- Matches aggregate class name to identity tag (case-insensitive)

## Creating a Custom Identity

```csharp
// 1. Define the identity class (name must end with "Id")
public class DocumentId : EventStoreIdentity
{
    public DocumentId(long id) : base(id) { }
    public DocumentId(string id) : base(id) { }
}

// 2. Register identities
var counterService = new CounterService(mongoDatabase);
var identityManager = new IdentityManager(counterService);
identityManager.RegisterIdentitiesFromAssembly(typeof(DocumentId).Assembly);

// 3. Generate new IDs
var newId = identityManager.New<DocumentId>();              // sync
var newId = await identityManager.NewAsync<DocumentId>();   // async
var batch = await identityManager.NewManyAsync<DocumentId>(10); // batch

// 4. Parse from string
var id = identityManager.ToIdentity<DocumentId>("Document_123");
if (identityManager.TryParse<DocumentId>("Document_42", out var parsed)) { }

// 5. Check if string matches identity format
bool isMatch = EventStoreIdentity.Match<DocumentId>("Document_99"); // true

// 6. JSON serialization
var converter = new EventStoreIdentityJsonConverter(identityManager);
var json = JsonConvert.SerializeObject(id, converter);
// Result: "Document_123"
```

## Design Notes

- **Thread Safety**: Counter service uses MongoDB atomic `FindOneAndUpdate`; static tag caches use `ConcurrentDictionary`
- **Caching**: Tags cached statically; counter validity uses LRU cache
- **Case Sensitivity**: Identity comparison is case-insensitive for tags, `Normalize()` fixes casing
- **Positive IDs Only**: All numeric IDs must be >= 0
- **Implicit String Conversion**: `EventStoreIdentity` has `implicit operator string`
- **Concurrency**: Translation/association handle race conditions with duplicate key recovery

## Exception

`JarvisFrameworkIdentityException` (serializable) - thrown for invalid format, unregistered types, tag mismatches, negative IDs.

## Test Files
- `Jarvis.Framework.Tests/SharedTests/IdentitySupport/EventStoreIdentityTests.cs`
- `Jarvis.Framework.Tests/SharedTests/IdentitySupport/IdentityManagerTests.cs`
- `Jarvis.Framework.Tests/SharedTests/IdentitySupport/IDentitySupportTests.cs`
- `Jarvis.Framework.Tests/SharedTests/IdentitySupport/AbstractIdentityTranslatorTests.cs`
- `Jarvis.Framework.Tests/SharedTests/IdentitySupport/StringToIdentityAssociatorTests.cs`
- `Jarvis.Framework.Tests/SharedTests/IdentitySupport/EventStoreIdentitySerializationTests.cs`
- `Jarvis.Framework.Tests/SharedTests/IdentitySupport/DictionaryAsObjectJarvisSerializerTests.cs`
- `Jarvis.Framework.Tests/SharedTests/IdentitySupport/AuxClasses.cs` (TestId, TestFlatId, etc.)
