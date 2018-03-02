A simple Multicast Domain Name Service based on [RFC 6762](https://tools.ietf.org/html/rfc6762).  The source code is on [GitHub](https://github.com/richardschneider/net-mdns) and the package is published on [NuGet](https://www.nuget.org/packages/Makaretu.Dns.Multicast).

The [MulticastService](xref:Makaretu.Dns.MulticastService) is used to send [queries](xref:Makaretu.Dns.MulticastService.SendQuery) and [answers](xref:Makaretu.Dns.MulticastService.SendAnswer).  It also listens for DNS [Messages](xref:Makaretu.Dns.Message) and raises either the [QueryReceived](xref:Makaretu.Dns.MulticastService.QueryReceived) or [AnswerReceived](xref:Makaretu.Dns.MulticastService.AnswerReceived) event.

