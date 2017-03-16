J.A.R.V.I.S. Framework - Proximo srl (c)
====

##Version 2:

###2.0.9

- Health check on FATAL error in Projection Engine. [Issue 22](https://github.com/ProximoSrl/Jarvis.Framework/issues/21)
- When a projection throws an error, only the corresponding slot is stopped. [Issue 21](https://github.com/ProximoSrl/Jarvis.Framework/issues/22) 

###2.0.8

- Some code cleanup.
- Reference to NEventStore now point to -beta packages, updated NES core to latest beta that improve logging in PollingClient2.
- Added health check on NES polling error. [Issue 23](https://github.com/ProximoSrl/Jarvis.Framework/issues/23)

###2.0.7

- Minor fixes on logging

###2.0.6

- Added better logging info on command execution (describe on error and userid).

###2.0.5

- Updated mongo driver to 2.4.0
- Fixed generation of new Abstract Identity
- Added flat serialization for AbstractIdentity

###2.0.4

- Fixed CommitEnhancer set version on events. It always set the last version of the commit.

###2.0.3

- Fixed an error in Test Specs cleanup in base test class.

###2.0.2

- Fixed bad count in command execution that causes not correct number of retries.

###2.0.1

- Changed references to nevenstore.

###Version 1

...
