# net-mdns

[![build status](https://ci.appveyor.com/api/projects/status/github/richardschneider/net-mdns?branch=master&svg=true)](https://ci.appveyor.com/project/richardschneider/net-mdns) 
[![travis build](https://travis-ci.org/richardschneider/net-mdns.svg?branch=master)](https://travis-ci.org/richardschneider/net-mdns)
[![CircleCI](https://circleci.com/gh/richardschneider/net-mdns.svg?style=svg)](https://circleci.com/gh/richardschneider/net-mdns)
[![Coverage Status](https://coveralls.io/repos/richardschneider/net-mdns/badge.svg?branch=master&service=github)](https://coveralls.io/github/richardschneider/net-mdns?branch=master)
[![Version](https://img.shields.io/nuget/v/Makaretu.Dns.Multicast.svg)](https://www.nuget.org/packages/Makaretu.Dns.Multicast)
[![docs](https://cdn.rawgit.com/richardschneider/net-mdns/master/doc/images/docs-latest-green.svg)](https://richardschneider.github.io/net-mdns/articles/intro.html)

A simple Multicast Domain Name Service based on [RFC 6762](https://tools.ietf.org/html/rfc6762).  Can be used 
as both a client (sending queries) or a server (responding to queries).

A higher level DNS Service Discovery based on [RFC 6763](https://tools.ietf.org/html/rfc6763) that automatically responds to any query for the 
service or service instance.

## Features

- Targets Framework 4.6.1, .NET Standard 1.4 and 2.0
- Supports IPv6 and IPv4 platforms
- CI on Circle (Debian GNU/Linux), Travis (Ubuntu Xenial and OSX) and AppVeyor (Windows Server 2016)
- Detects new and/or removed network interfaces
- Supports multicasting on multiple network interfaces
- Supports reverse address mapping
- Supports service subtypes (features)
- Handles legacy unicast queries, see #61

## Getting started

Published releases are available on [NuGet](https://www.nuget.org/packages/Makaretu.Dns.Multicast/).  To install, run the following command in the [Package Manager Console](https://docs.nuget.org/docs/start-here/using-the-package-manager-console)

    PM> Install-Package Makaretu.Dns.Multicast
    
or using .NET CLI run the following command in the project folder

    > dotnet add package Makaretu.Dns.Multicast
    
## Usage Service Discovery

### Advertising

Always broadcast the service ("foo") running on local host with port 1024.

```csharp
using Makaretu.Dns;

var service = new ServiceProfile("x", "_foo._tcp", 1024);
var sd = new ServiceDiscovery();
sd.Advertise(service);
```

See the [example advertiser](Spike/Program.cs) for a working program.

### Discovery

Find all services running on the local link.

```csharp
using Makaretu.Dns;

var sd = new ServiceDiscovery();
sd.ServiceDiscovered += (s, serviceName) => { // Do something };
```

Find all service instances running on the local link.

```csharp
using Makaretu.Dns;

var sd = new ServiceDiscovery();
sd.ServiceInstanceDiscovered += (s, e) => { // Do something };
```

See the [example browser](Browser/Program.cs) for a working program.

## Usage Multicast

### Event Based Queries

Get all the Apple TVs. The query is sent when a network interface is discovered. 
The `AnsweredReceived` callback contains any answer that is seen, not just the answer
to the specific query.

```csharp
using Makaretu.Dns;

var mdns = new MulticastService();
mdns.NetworkInterfaceDiscovered += (s, e) => mdns.SendQuery("appletv.local");
mdns.AnswerReceived += (s, e) => { // do something with e.Message };
mdns.Start();
```

### Async Queries

Get the first answer to Apple TVs. Wait 2 seconds for an answer.

```csharp
using Makaretu.Dns;

var service = "appletv.local";
var query = new Message();
query.Questions.Add(new Question { Name = service, Type = DnsType.ANY });
var cancellation = new CancellationTokenSource(2000);

using (var mdns = new MulticastService())
{
    mdns.Start();
    var response = await mdns.ResolveAsync(query, cancellation.Token);
    // Do something
}
```

### Broadcasting

Respond to a query for the service.  Note that `ServiceDiscovery.Advertise` is much easier.

```csharp
using Makaretu.Dns;

var service = "...";
var mdns = new MulticastService();
mdns.QueryReceived += (s, e) =>
{
    var msg = e.Message;
    if (msg.Questions.Any(q => q.Name == service))
    {
        var res = msg.CreateResponse();
        var addresses = MulticastService.GetIPAddresses()
            .Where(ip => ip.AddressFamily == AddressFamily.InterNetwork);
        foreach (var address in addresses)
        {
            res.Answers.Add(new ARecord
            {
                Name = service,
                Address = address
            });
        }
        mdns.SendAnswer(res);
    }
};
mdns.Start();
```

## Related projects

- [net-dns](https://github.com/richardschneider/net-dns) - DNS data model and Name Server with serializer for the wire and master file format
- [net-udns](https://github.com/richardschneider/net-udns) - client for unicast DNS, DNS over HTTPS (DOH) and DNS over TLS (DOT)

## License
Copyright © 2018-2019 Richard Schneider (makaretu@gmail.com)

The package is licensed under the [MIT](http://www.opensource.org/licenses/mit-license.php "Read more about the MIT license form") license. Refer to the [LICENSE](https://github.com/richardschneider/net-mdns/blob/master/LICENSE) file for more information.

<a href="https://www.buymeacoffee.com/kmXOxKJ4E" target="_blank"><img src="https://www.buymeacoffee.com/assets/img/custom_images/yellow_img.png" alt="Buy Me A Coffee" style="height: auto !important;width: auto !important;" ></a>
