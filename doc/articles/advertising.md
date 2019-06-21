# Advertising

[ServiceDiscovery.Advertise](xref:Makaretu.Dns.ServiceDiscovery.Advertise*)
is used to generate all the DNS [resource records](xref:Makaretu.Dns.ResourceRecord) needed to
answer a Multicast DNS or DNS-SD query for a [service profile](xref:Makaretu.Dns.ServiceProfile).

### Usage
```csharp
var profile = new ServiceProfile("me", "_myservice._udp", 1234, 
  new IPAddress[] { IPAddress.Loopback });
profile.Subtypes.Add("apiv2");
profile.AddProperty("someprop", "somevalue");

var sd = new ServiceDiscovery();
sd.Advertise(profile);
```

### Resource records

The following resource records are generated from the above profile.

```
_services._dns-sd._udp.local     PTR _myservice._udp.local
_myservice._udp.local            PTR me._myservice._udp.local
apiv2._sub._myservice._udp.local PTR me._myservice._udp.local

me._myservice._udp.local SRV 0 0 1234 me.myservice.local
me._myservice._udp.local TXT txtvers=1 someprop=somevalue
me.myservice.local       A 127.0.0.1

1.0.0.127.in-addr.arpa PTR me.myservice.local
```
