# Multicast Service

The [MulticastService](xref:Makaretu.Dns.MulticastService) implements [RFC 6762 - Multicast DNS](https://tools.ietf.org/html/rfc6762"). It
sends and receives messages via multicast; basically broadcasting
the message to all interested parties on the link local network(s).
A message is a standard [DNS message](https://richardschneider.github.io/net-dns/articles/message.html).

## Sending messages

The [SendQuery](xref:Makaretu.Dns.MulticastService.SendQuery*) and 
[SendAnswer](xref:Makaretu.Dns.MulticastService.SendAnswer*) methods
are used to send a message.  Note that the sending MulticastService
will also receive the message.

```csharp
using Makaretu.Dns;

var mdns = new MulticastService();
mdns.SendQuery("appletv.local");
```

## Receiving messages

The [QueryReceived](xref:Makaretu.Dns.MulticastService.QueryReceived) or [AnswerReceived](xref:Makaretu.Dns.MulticastService.AnswerReceived) event
is raised whenever a message is received.

```csharp
using Makaretu.Dns;

var mdns = new MulticastService();
mdns.AnswerReceived += (s, e) => 
{ 
   // do something with e.Message 
};
mdns.Start();
```

## Duplicate messages