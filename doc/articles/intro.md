Simple Multicast DNS for C# and F#.  The source code is on [GitHub](https://github.com/richardschneider/net-mdns) 
and the package is published on [NuGet](https://www.nuget.org/packages/Makaretu.Dns.Multicast).

It conforms to
- [RFC 6762 - Multicast DNS](https://tools.ietf.org/html/rfc6762)
- [RFC 6761 - DNS-Based Service Discovery](https://tools.ietf.org/html/rfc6763)

The above RFCs are commonly referred to as [Bonjour](https://developer.apple.com/bonjour/) or 
[Zero Config](http://www.zeroconf.org/).

The [MulticastService](xref:Makaretu.Dns.MulticastService) is used to send [DNS](https://en.wikipedia.org/wiki/Domain_Name_System)
[queries](xref:Makaretu.Dns.MulticastService.SendQuery) and 
[answers](xref:Makaretu.Dns.MulticastService.SendAnswer) over the link local network.
It also listens for DNS [Messages](xref:Makaretu.Dns.Message) and raises either the 
[QueryReceived](xref:Makaretu.Dns.MulticastService.QueryReceived) or [AnswerReceived](xref:Makaretu.Dns.MulticastService.AnswerReceived) event.

To broadcast a service, simply create a [ServiceProfile](xref:Makaretu.Dns.ServiceProfile) 
and then [Advertise](xref:Makaretu.Dns.ServiceDiscovery.Advertise) it.  This then responnds to any queries
about any service, the service, or the service instance.