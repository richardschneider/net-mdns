Simple Multicast DNS for C# and F#.  The source code is on [GitHub](https://github.com/richardschneider/net-mdns) 
and the package is published on [NuGet](https://www.nuget.org/packages/Makaretu.Dns.Multicast).

It conforms to
- [RFC 6762 - Multicast DNS](https://tools.ietf.org/html/rfc6762)
- [RFC 6761 - DNS-Based Service Discovery](https://tools.ietf.org/html/rfc6763)

The above RFCs are commonly referred to as [Bonjour](https://developer.apple.com/bonjour/) or 
[Zero Config](http://www.zeroconf.org/).

## Multicast Service

The [MulticastService](ms.md) is used to send DNS
[queries](xref:Makaretu.Dns.MulticastService.SendQuery*) and 
[answers](xref:Makaretu.Dns.MulticastService.SendAnswer*) over the link local network.
It also listens for DNS [Messages](xref:Makaretu.Dns.Message) and raises either the 
[QueryReceived](xref:Makaretu.Dns.MulticastService.QueryReceived) or [AnswerReceived](xref:Makaretu.Dns.MulticastService.AnswerReceived) event.

## Service Discovery

The [ServiceDiscovery](sd.md) is used to find services and service instances on the network.
To advertise a service, simply create a [ServiceProfile](xref:Makaretu.Dns.ServiceProfile) 
and then [Advertise](advertising.md) it.  Any queries for the service or 
service instance will be answered with information from the profile.


## Related projects

- [net-dns](https://github.com/richardschneider/net-dns) - DNS data model and Name Server with serializer for the wire and master file format
- [net-udns](https://github.com/richardschneider/net-udns) - client for unicast DNS, DNS over HTTPS (DOH) and DNS over TLS (DOT)
