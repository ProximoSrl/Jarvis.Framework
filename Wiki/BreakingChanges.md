#Version 2.0

##Version 2.1

1. Change to iconstruct aggregate

Interface *IConstructAggregatesEx* has now two methods, one to create the Aggregate, and the other to restore snapshot to the Aggregate. All the call to Build() method should be changed as following.

The third parameter (the instance of the snapshot) is removed. If the third parameter is different from null you need to call the ApplySnapshot method passing the instance of the Aggregate returned from call to Build and the instance of the snapshot.

##Version 2.0.0

###Core

####Complete refactor of BusBootstrapper

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

####Notification transformer for ICollectionWrapper

ICollectionWrapper interface renamed property PrepareForNotification to TransformForNotification to reflect the fact that now the projection is able to change the notification object that is sent when a readamodel is updated.

This is necessary for really big readmodel, where probably the UI is interested only in a very few set of property and not the entire object.

