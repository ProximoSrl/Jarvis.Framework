J.A.R.V.I.S. Framework - Proximo srl (c)
====

## 3.1.0

- DotNetCore support.

## 3.0.0

- Migration to NStore
- Migration to async.
- Entity support.

### 3.1.0

- Introduced interface ICounterServiceWithOffset

### 3.2.0

- Classes that inhertis from Repository command handler can add custom headers on che changeset.
- Ability to create MongoCollection wrapper disabling nitro.
- IdentityManager now is case insensitive (3.2.8)

## 2.1.0

- Offline support for command execution
- Initial support to create Host application to host services 

#### Breaking changes

- IMessagesTracker methods now accepts entire command and not only the commandId. 
- [Projection need to use attribute for signature/name/slot information](Wiki/BreakingChanges/2.1.0_ProjectionAttribute.md)

## 2.0

First version of the changelog.

