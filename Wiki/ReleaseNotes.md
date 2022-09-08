# J.A.R.V.I.S. Framework - Proximo srl (c)

====
## 6.3.7

- Restored rebus test.
- Fixed a bug in bug messages tracking.
 
## 6.3.6

- Updated NStore and Mongodb drivers

## 6.3.5

- Fix handle poison messages in queue that are not commands.

## 6.3.4

- Fix build using a standard build.ps1 file
- Rename of rebus project
  
## 6.3.3

- Fix healt check when loggin with no string format.

## 6.3.2

- AbstractIdentityTranslator: added GetAliases(key) function that returns all the aliases for a mapped key.
- AbstractIdentityTranslator: fixed GetAliases(key[]) function so that does not throw exception when there are multiple mappings for the same key (it will return the first mapped value).

## 6.3.1

- Fix CreateSlotAtCheckpoint function to set Current to the same value

## 6.3.0

- Case insensitive EventStoreIdentities

## 6.2.1

- Updated references to rebus.mongob

## 6.2.0

- RebuildEngine 2 that does not uses unwinded events.
- Updated all references.

## 6.1.7

- Better indexing for readmodel fixer

## 6.1.6

- Better indexing for atomic readmodel.

## 6.1.5

- MSMQ Health check.
  
## 6.1.4

- DomainEvent context is now a readonly dictionary.
  
## 6.1.3

- DomainEvent CommitStamp is now a property in pure getter (not anymore set by commit enhancer).
  
## 6.1.2

- Added helpers to better serialization of command headers.

## 6.1.1

- Restored public properties for atomic readmodel.

## 6.1.0

- Override user id and timestamp on commands (on-behalf-of)
- Added AggregateVersion on Standard Readmodels.

## Version 6

### 6.0.11

- Avoid scanning multiple time asembly for aggregates

### 6.0.10

- Better check on EventStore identity constructor
- Fixed skewing slots in Projection engine on new projection
- Fixed interrupting rebuild #11561
- Fixed serialization error in command executor 

### 6.0.9

- Fix time unit for some internal metrics
  
### 6.0.9

- App.Metrics meters now uses seconds as standard time unit.
  
### 6.0.8

- Fixed Metrics wrapper.
  
### 6.0.7

- Fixed unit tests.

### 6.0.6

- Better health check for commit polling client.
  
### 6.0.3

- Added ability to disable async on ConcurrentCheckpointTracker due to a mongodb driver anomaly.

### 6.0.2

- Update references
- Fixed nuspec
- Added ability to remove metrics
- Added ability to parametrize concurrent checkpoin tracker flush strategy.

### 6.0.1

- Update references.

### 6.0.0

- Tested and used with .NET core projects.
- Removed Metrics.NET in favor of [App Metrics](https://www.app-metrics.io/)
- Support usage of Rebus MongoDb transport
- Support for Domain Event upcaster.
- Updated references and readme
- Moved to app.metrics, consult breaking changes in [dedicated wiki page](Metrics/metrics.md)

## Version 5

# 5.3.9

- Fix ProjectionStatusLoader to work in memory to read the most up-to-date projection checkpoint.

### 5.3.5

- Better checkpoint handling for slots that does not handle an event in projection service

### 5.3.4

- Added Upcasting logic (we had everything in place but not the infrastructure for upcasting)

### 5.3.3

- No change, needed to bump the verison for a problem with nuget packages.

### 5.3.2

- AtomicReadmodel: Fix FindOneAndCatchup, that did not project anything if the first event was not already projected by the engine.

### 5.3.1

- Fixed builds

### 5.3.0

- AtomicReadmodel: Catchup readmodel to the latest version in CollectionWrapper.

### 5.2.0

- AtomicReadmodel: Before and after events handler

### 5.1.0

- Updated references
- Global option to have a better catchup for atomic readmodels (JarvisFrameworkGlobalConfiguration.AtomicProjectionEngineOptimizedCatchup)

### 5.0.1

- Fixed query for tracking message model pending plus other fix in message tracking area.

### 5.0.0

- Refactor of Tracking Message Model (see breaking changes)

## Version 4

Main modification is: Moved framework to .NETStandard. 

### Version 4.3.2

- Fix bug in LiveAtomicReadModelProcessor reconstruction of AtomicReadmodel.

### Version 4.3.1

- Fix EventStoreIdentity Match<T> method

### Version 4.3.0

- Atomic readmodel
- Some command handler features to support draftable
- Removed StreamProcessorManagerFactory

### Version 4.2.x

- Added global events for Metrics to allow intercept metrics reporting.
- Modified context logging adding explicit correlation id.

### Version 4.1.x

- Added reverse translation of array of Ids in AbstractIdentityTranslator.
- Added multiple mapping in AbstractIdentityTranslator.

### Version 4.0.x

- Upgraded project for .NET standard
- Changed TransformForNotification in CollectionWrapper to use also the event that generates change in the readmodel.

## Version 3

### 3.3.0

- Various bugfixes.
- Changed Notification manipulation for CollectionWrapper including event.
- Added a property to mark last event of a specific commit during projection
- Added a filter in CollectionWrapper to generate notification only for last event of a commit.

### 3.2.0

- Classes that inhertis from Repository command handler can add custom headers on che changeset.
- Ability to create MongoCollection wrapper disabling nitro.
- IdentityManager now is case insensitive (3.2.8)

### 3.1.0

- Introduced interface ICounterServiceWithOffset
- DotNetCore support.

## 3.0.0

- Migration to NStore
- Migration to async.
- Entity support.


## Version 2:

### 2.1.0

- Support for offline
- Attribute for IProjection Properties [breaking change](BreakingChanges/2.1.0_ProjectionAttribute.md)

### 2.0.9

- Health check on FATAL error in Projection Engine. [Issue 22](https://github.com/ProximoSrl/Jarvis.Framework/issues/21)
- When a projection throws an error, only the corresponding slot is stopped. [Issue 21](https://github.com/ProximoSrl/Jarvis.Framework/issues/22) 

### 2.0.8

- Some code cleanup.
- Reference to NEventStore now point to -beta packages, updated NES core to latest beta that improve logging in PollingClient2.
- Added health check on NES polling error. [Issue 23](https://github.com/ProximoSrl/Jarvis.Framework/issues/23)

### 2.0.7

- Minor fixes on logging

### 2.0.6

- Added better logging info on command execution (describe on error and userid).

### 2.0.5

- Updated mongo driver to 2.4.0
- Fixed generation of new Abstract Identity
- Added flat serialization for AbstractIdentity

### 2.0.4

- Fixed CommitEnhancer set version on events. It always set the last version of the commit.

### 2.0.3

- Fixed an error in Test Specs cleanup in base test class.

### 2.0.2

- Fixed bad count in command execution that causes not correct number of retries.

### 2.0.1

- Changed references to nevenstore.

### Version 1

...
