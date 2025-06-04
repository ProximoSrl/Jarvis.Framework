# Version 7

## vNext

- All methods from ILiveAtomicReadModelProcessorEnhanced and ILiveAtomicReadModelProcessor now ended with Async and does not contains async in the name ex: instead of ProcessAsyncUntilChunkPosition is ProcessUntilChunkPositionAsync


## 7.10.0

- Updated mongodb driver 3.1.0

## 7.9.0

- Small fixes

## 7.8.5

- All methods from ILiveAtomicReadModelProcessorEnhanced and ILiveAtomicReadModelProcessor now ended with Async and does not contains async in the name ex: instead of ProcessAsyncUntilChunkPosition is ProcessUntilChunkPositionAsync

## 7.8.2

- Default version of LINQ driver for Mongodb is version 3. To revert to default you need to call JarvisFrameworkGlobalConfiguration.EnableMongodbDriverLinq2() to re-enable linq2 driver.

## 7.7.10

- GuaranteedDeliveryBroadcastBlock allows for meter name to be specified.

## 7.7.6

- Changed IMongoQueryInterceptorConsumer to allow for better interception.

## 7.7.0

- When init MetricsHelper you need to pass a complete build app.metrics configuration
- Removed EventHappenedEventArgs due to metrics consolidation, now framework uses metrics directly
- Removed completely aggregate cached repository, because it introuces a lots of complexity.
- ProcessManagerDispatcher does not implements anymore ISTartable, you need to start manually.
- Mongodb driver updated to 2.19.0, please read [Release notes](https://github.com/mongodb/mongo-csharp-driver/releases/tag/v2.19.0)

## 6.3.0

- EventStoreIdentities are now case insensitive, you can create a DocumentId from string "documeNTID_12"
- IdentityManager.ToIdentity now raise a JarvisFrameworkIdentityException exception if the id is malformed not the old and not specific JarvisFrameworkEngineException

### 6.3.4

- Renamed all namespace from Jarvis.Framework.Bus.Rebus.Integration to Jarvis.Framework.Rebus

## 6.1.0

- AbstractAtomicReadModel moved some property to private (you should not modify them): CreationUser, LastModificationUser, LastModify, AggregateVersion
- Standard reamodel base class now contains AggregateVersion property

# Version 5

## 5.4.0

- Moved to app.metrics, consult breaking changes in [dedicated wiki page](Metrics/metrics.md)

## 5.2.0

- Removed Jarvis Startable facitlities, it was moved into separate package Castle Utilities.

## 5.1.0

- IAtomicReadmodelChangesetConsumer renamed to IAtomicReadmodelProjectorHelper with also a rename for all factory classes.

## 5.0.0

Moved IMessagesTracker to namespace Jarvis.Framework.Shared.Commands.Tracking

# Version 4

Main modification is: Moved framework to .NETStandard. 

## Version 4.3.0

- Removed StreamProcessorManagerFactory, it was a class that is really not used anymore.

## Version 4.0.0

- Changed TransformForNotification in CollectionWrapper to use also the event that generates change in the readmodel.
 
# Version 3

The overall major change of version 3 is the migration to NStore, async and a major refactoring. There are many breaking changes with version 2.


**Migration to NSTORE, the whole framework was changed**

- Invariant check on state is done with a function with signature *protected override InvariantsCheckResult OnCheckInvariants()*

## Version 3.0.1

- Changed retry for command handler, needs to register **ICommandExecutionExceptionHelper** interface

## Version 3.2

- Bugfix and added ability to create mongo collection without nitro.
 
# Version 2.0

## Version 2.1

### Change to iconstruct aggregate

Interface *IConstructAggregatesEx* has now two methods, one to create the Aggregate, and the other to restore snapshot to the Aggregate. All the call to Build() method should be changed as following.

The third parameter (the instance of the snapshot) is removed. If the third parameter is different from null you need to call the ApplySnapshot method passing the instance of the Aggregate returned from call to Build and the instance of the snapshot.

### Introduction of attribute for specifying projection information

Now you need to use an attribute to specify Signature, Slot and common Name of projection. [Read how to migrate code here](BreakingChanges/2.1.0_ProjectionAttribute.md).

## Version 2.0.0

### Core

#### Complete refactor of BusBootstrapper

BusBoostrapper has only the duty of registering IBus but now implements IStartable, then you should register BusStarter if you want to automatically start the bus with the IStartable interface.

The typical usage pattern is to register both classes with this code. BusBootstrapper is usually registered with priority high, then all the IStartable that register the IMessageHandler with highest priority, and the bus start has priority normal. All startable that depends on IBus to be registered should have priority Normal or lower, all startable that depends on IBus and needed for the IBus to be **started** should declare a priority lesser than Normal.


	Component
	    .For<BusBootstrapper>()
	    .DependsOn(Dependency.OnValue<IWindsorContainer>(container))
	    .DependsOn(Dependency.OnValue("connectionString", ConfigurationServiceClient.Instance.GetSetting("connectionStrings.rebus")))
	    .DependsOn(Dependency.OnValue("prefix", busPrefix))
	    .WithStartablePriorityHigh(),
	Component
	    .For<BusStarter>(),

#### Notification transformer for ICollectionWrapper

ICollectionWrapper interface renamed property PrepareForNotification to TransformForNotification to reflect the fact that now the projection is able to change the notification object that is sent when a readamodel is updated.

This is necessary for really big readmodel, where probably the UI is interested only in a very few set of property and not the entire object.

### Message Tracking

The message tracking Schema has been changed:

- the field 'ErrorMessages' has been removed.
- a 'Type' field has been added, must be set to '1' (Command) if the 'ExecutionCount' field is >= 1, otherwise set it to '2' (Event)
- a 'MessageType' field has been added, must be set to string type name representation without the namespace (in the previous schema take the value from 'Message._t' stripping the last '_X' part added by mongo), this a good guess, to have the correct value we should deserialize the class.
- a 'Completed' field has been added, this should be set to 'true' for completed commands (in the previos scheme every command that had a CompletedAt or FailedAt field)
- a 'Success' field has been added, this should be set to 'true' for successfully completed commands (in the previous scheme every command that had a CompletedAt field)
- the field 'FailedAt' has been removed, the information should be migrated over 'CompletedAt'

run the following script to convert from the old format to the new one:

	var requests = [];
	db.messages.find({}).snapshot().forEach(function(document) { 
		// we compose the $set object dynamically
        var $set = {};

		// MessageType typeof(Command)
        var msgType = document.Message._t.split("_")[0];
        $set.MessageType = msgType;

        // Type: Command (1) or Event (2)
        var type = null;
        if (parseInt(document.ExecutionCount) >= NumberInt(1)) {
        	type = NumberInt(1); // Command
        } else {
        	type = NumberInt(2); // Event
        }
		set.Type = type;
		
        // Completed (only for commands)
        if (type == 1) {
            var completed = document.CompletedAt != null || document.FailedAt != null;
            $set.Completed = completed;
        }
        // Success (only for commands)
        if (type == 1) {
            var success = document.CompletedAt != undefined;
            $set.Success = success;
        }
        // FailedAt has been removed
        var completedAt = document.CompletedAt;
        if (document.FailedAt != null && document.CompletedAt == null) {
        	completedAt = document.FailedAt;
        }
        if (completedAt != null) {
            $set.CompletedAt = completedAt;
        }
                
		requests.push( { 
			'updateOne': {
				'filter': { '_id': document._id },
				'update': { 
                    '$unset': { 'ErrorMessages': 1},
                    '$set': $set
				}
			}
		});
		if (requests.length === 1000) {
			//Execute per 1000 operations and re-init
			db.messages.bulkWrite(requests);
			requests = [];
		}
	});
	if(requests.length > 0) {
		db.messages.bulkWrite(requests);
	}
	

references:

- to 'copy' the value of one field to another: http://stackoverflow.com/questions/2606657/update-field-with-another-fields-value-in-the-document