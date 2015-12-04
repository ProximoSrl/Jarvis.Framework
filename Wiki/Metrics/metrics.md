##Why metrics .NET

Library [Metrics.NET](https://github.com/etishor/Metrics.NET) is a library to generate metrics for your application. It isused by jarvis framework to give you some metrics about some basic funcionality of the framework itself.

###How to host as HTTP

You can follow standard documentation in [Metrics.NET Wiki](https://github.com/etishor/Metrics.NET/wiki), but actually to start a service in your code this is all the code you need.

```csharp
 Metric
	.Config
	.WithHttpEndpoint(binding)
```

You need to pay particular attenction to administrative privilege. You can incurr in an *Error Configuring HTTP Listener - Access is denied* error if you run your code without administrative privilege. As stated [here](https://github.com/etishor/Metrics.NET/issues/38) you should bind to **http://+:portnumber/** address also you should allow usage of that port to the user that runs the program (or all the users) with the command 

``````csharp
netsh http add urlacl url=http://BIGEND:55557/ user=Everyone
```

Launched by an admin console. 

###What Metrics Jarvis.Framework can offer

- [Projections](projections.md)