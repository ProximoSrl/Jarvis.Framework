# Jarvis.Framework AI Coding Guide

## Architecture Overview

Jarvis.Framework is a CQRS/Event Sourcing framework built on NStore, MongoDB, and Castle Windsor. It provides infrastructure for building event-sourced applications with projections, atomic readmodels, and command handling.

### Core Components

- **Jarvis.Framework.Shared**: Base types, identity system, events, commands, readmodels
- **Jarvis.Framework**: Kernel with projection engines, command handlers, aggregate roots
- **Jarvis.Framework.Rebus**: Integration with Rebus message bus
- **Jarvis.Framework.TestHelpers**: Testing utilities including `AggregateSpecification<>`

### Key Patterns

**Event Sourcing with NStore**: Aggregates inherit from `AggregateRoot<TState, TId>` where state is `JarvisAggregateState`. Events are `DomainEvent` subclasses. Use `RaiseEvent()` to emit events.

**Identity System**: All aggregate identities inherit from `EventStoreIdentity` (case-insensitive). Format is `Prefix_123` (e.g., `Document_42`). Identity class names must end with "Id" - the prefix is derived by removing "Id" suffix.

**Command Handlers**: Extend `RepositoryCommandHandler<TAggregate, TCommand>`. Use `FindAndModifyAsync(id, aggregate => {...})` to load and modify aggregates. Repository auto-clears between commands.

**Atomic Readmodels**: Inherit from `AbstractAtomicReadModel`. Projections via `On(EventType evt)` methods. Engine automatically tracks versions and handles idempotency. Use `AtomicProjectionEngine` for standalone projection. These readmodels represent single aggregate views and are self-contained.

**Standard Readmodels**: Inherit from `AbstractReadModel<TKey>` for cross-aggregate projections. Use `ICollectionWrapper<TModel, TKey>` for persistence with MongoDB. Handled by `ProjectionEngine`.

## Critical Setup & Configuration

**MongoDB Serialization (CRITICAL)**: Must call `MongoFlatMapper.EnableFlatMapping()` as the **very first line** in your application startup, then register message assemblies via `MessagesRegistration.RegisterAssembly(typeof(YourType).Assembly)`. Order is critical - flat mapping first, then assembly registration.

**Dependency Injection**: Uses Castle Windsor. Register components with lifecycle management (Singleton, Transient, PerWebRequest).

**Versioning**: Use `[VersionInfo(X)]` attribute on events/commands. Discriminators in MongoDB will be `EventName_X`. Default version is 1 if not specified.

**GitVersion**: Version management via GitVersion.yml. Build script (`build.ps1`) handles assembly versioning. Use GitFlow branching (develop/feature/release/hotfix).

## Build & Test Workflow

- If you need to run tests, please run with dotnet test specifying only the project that contains the tests
- If possible when you need to run a test run test only the class that you are modifying using the filter --filter FullyQualifiedName~Namespace.ClassName 
- If you previously build the solution and the project use the --no-build option to skip the build phase

**Custom NuGet Feed**: Uses MyGet feed "jarvis" for internal packages (configured in `NuGet.Config`).

## Project-Specific Conventions

**Projection Attributes**: Projections must use `[ProjectionInfo]` attribute to specify Signature, Slot, and CommonName (introduced in v2.1).

**Invariant Checks**: Implement `OnCheckInvariants()` on aggregate state, returning `InvariantsCheckResult`.

**Async Naming (v7+)**: All async methods end with `Async` suffix (e.g., `ProcessUntilChunkPositionAsync`). CancellationToken parameters added to all async interfaces.

**MongoDB LINQ**: Default is LINQ v3 driver (since v7.8.2). Call `JarvisFrameworkGlobalConfiguration.EnableMongodbDriverLinq2()` to revert.

**Metrics**: Uses App.Metrics (migrated from Metrics.NET in v6.0). Configure via `MetricsHelper` with complete IMetrics configuration.

## Common Pitfalls

- **Forgetting MongoFlatMapper**: Results in "Unknown discriminator" errors or nested identity objects instead of flat strings
- **Wrong assembly registration order**: Calling `RegisterAssembly()` before `EnableFlatMapping()` breaks serialization
- **Identity naming**: Class names must end with "Id" or constructor throws `JarvisFrameworkIdentityException`
- **Out-of-order projection**: AtomicReadModel throws `JarvisFrameworkEngineException` if changeset versions are dispatched out of order
- **Missing [VersionInfo]**: Events without version attributes default to version 1, causing discriminator mismatches if you later add versioning

## Key Files & Entry Points

- **Build**: `build.ps1` - main build script
- **Global config**: `JarvisFrameworkGlobalConfiguration.cs` - framework settings
- **Command execution**: `AbstractCommandHandler.cs`, `RepositoryCommandHandler.cs`
- **Projection engines**: `ProjectionEngine.cs`, `AtomicProjectionEngine.cs`
- **Identity base**: `EventStoreIdentity.cs` - identity system foundation
- **Aggregate base**: `AggregateRoot.cs` - event sourcing aggregate root
- **Test helpers**: `AggregateSpecification.cs` - BDD-style aggregate testing with Machine.Specifications pattern

## Testing Patterns

Tests use `AggregateSpecification<TAggregate, TState, TId>` base class. Create aggregates with `Create(id)`, check raised events with `EventHasBeenRaised<TEvent>()` and `RaisedEvent<TEvent>()`. Use Machine.Specifications style with `Establish`, `Because`, `It` blocks.
