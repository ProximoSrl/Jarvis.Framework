##Projection Metrics

###Health status for slots

You can configure a couple of classes useful to gather health status of projection slots and automatically configure Metrics.NET with some useful stats.

```csharp
container.Register(
    Component
        .For<IProjectionStatusLoader>()
        .ImplementedBy<ProjectionStatusLoader>()
        .DependsOn(Dependency.OnComponent("eventStoreDatabase", "EventStoreDb"))
        .DependsOn(Dependency.OnComponent("readModelDatabase", "ReadModelDb")),
    Component
        .For<ProjectionMetricsConfigurer>()
);
```

With the above code we registered a component called *IProjectionStatusLoader* that depends both on EventStore database and ReadModel database. This component allows asking the following question: How many commit each slot needs to process?

The second component, called *ProjectionMetricsConfigurer* depends on *IProjectionStatusLoader* and simply configure Metrics.NET and Health check. It depends from a Int32 value called **maxSkewForSlot** used to specify how many commit we can tolerate a slot before considering it to be in un-healty state. Default value is 100;