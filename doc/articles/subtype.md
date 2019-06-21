# Subtypes

Subtypes are used to define features implemented by a service instance. See
[RFC 6763 - 7.1 Selective Instance Enumeration (Subtypes)](https://tools.ietf.org/html/rfc6763#section-7.1) for the details. 


## Finding service instances

[QueryServiceInstances](xref:Makaretu.Dns.ServiceDiscovery.QueryServiceInstances*) is used to find the
all the instances of a service with a specific feature. 
The [ServiceInstanceDiscovered](xref:Makaretu.Dns.ServiceDiscovery.ServiceInstanceDiscovered) event is raised 
each time a service instance is discovered.

```csharp
using Makaretu.Dns;

var sd = new ServiceDiscovery();
sd.ServiceInstanceDiscovered += (s, e) =>
{
    Console.WriteLine($"service instance '{e.ServiceInstanceName}'");
};
sd.QueryServiceInstances("_myservice", "apiv2");
```

## Advertising

Create a [ServiceProfile](xref:Makaretu.Dns.ServiceProfile) with a feature
and then [Advertise](xref:Makaretu.Dns.ServiceDiscovery.Advertise*) it.  Any queries for the service or 
service instance will be answered with information from the profile.

```csharp
using Makaretu.Dns;

var profile = new ServiceProfile("x", "_myservice._udp", 1024);
profile.Subtypes.Add("apiv2");
var sd = new ServiceDiscovery();
sd.Advertise(profile);
```