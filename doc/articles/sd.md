# Service Discovery

[ServiceDiscovery](xref:Makaretu.Dns.ServiceDiscovery) implements [RFC 6763 - DNS-Based Service Discovery](https://tools.ietf.org/html/rfc6763"). It
is used to find services and service instances on the link local network(s).

## Finding all services

[QueryAllServices](xref:Makaretu.Dns.ServiceDiscovery.QueryAllServices) is used to find the services. 
The [ServiceDiscovered](xref:Makaretu.Dns.ServiceDiscovery.ServiceDiscovered) event is raised 
each time a service is discovered.

```csharp
using Makaretu.Dns;

var sd = new ServiceDiscovery();
sd.ServiceDiscovered += (s, serviceName) =>
{
    Console.WriteLine($"service '{serviceName}'");
};
sd.QueryAllServices();
```

## Finding service instances

[QueryServiceInstances](xref:Makaretu.Dns.ServiceDiscovery.QueryServiceInstances*) is used to find the
all the instances of a service. 
The [ServiceInstanceDiscovered](xref:Makaretu.Dns.ServiceDiscovery.ServiceInstanceDiscovered) event is raised 
each time a service instance is discovered.

```csharp
using Makaretu.Dns;

var sd = new ServiceDiscovery();
sd.ServiceInstanceDiscovered += (s, e) =>
{
    Console.WriteLine($"service instance '{e.ServiceInstanceName}'");
};
sd.QueryServiceInstances("_myservice");
```

## Advertising

Create a [ServiceProfile](xref:Makaretu.Dns.ServiceProfile) 
and then [Advertise](xref:Makaretu.Dns.ServiceDiscovery.Advertise*) it.  Any queries for the service or 
service instance will be answered with information from the profile.

```csharp
using Makaretu.Dns;

var profile = new ServiceProfile("x", "_myservice._udp", 1024);
var sd = new ServiceDiscovery();
sd.Advertise(profile);
```