# net-mdns

[![build status](https://ci.appveyor.com/api/projects/status/github/richardschneider/net-mdns?branch=master&svg=true)](https://ci.appveyor.com/project/richardschneider/net-mdns) 
[![travis build](https://travis-ci.org/richardschneider/net-mdns.svg?branch=master)](https://travis-ci.org/richardschneider/net-mdns)
[![CircleCI](https://circleci.com/gh/richardschneider/net-mdns.svg?style=svg)](https://circleci.com/gh/richardschneider/net-mdns)
[![Coverage Status](https://coveralls.io/repos/richardschneider/net-mdns/badge.svg?branch=master&service=github)](https://coveralls.io/github/richardschneider/net-mdns?branch=master)
[![Version](https://img.shields.io/nuget/v/Makaretu.Dns.Multicast.svg)](https://www.nuget.org/packages/Makaretu.Dns.Multicast)
[![docs](https://cdn.rawgit.com/richardschneider/net-mdns/master/doc/images/docs-latest-green.svg)](https://richardschneider.github.io/net-mdns/articles/intro.html)

A simple Multicast Domain Name Service based on [RFC 6762](https://tools.ietf.org/html/rfc6762).  Can be used as both a client (sending queries) or a server (responding to queries).

## Features

- Targets .NET Standard 1.4 and 2.0
- Supports IPv6 and IPv4 platforms
- CI on Circle (Debian GNU/Linux), Travis (Ubuntu Trusty) and AppVeyor (Windows Server 2016)
- Periodically checks for new network interfaces

## Getting started

Published releases are available on [NuGet](https://www.nuget.org/packages/Makaretu.Dns.Multicast/).  To install, run the following command in the [Package Manager Console](https://docs.nuget.org/docs/start-here/using-the-package-manager-console).

    PM> Install-Package Makaretu.Mdns
    
## Usage

Get all the Apple TVs. The query is sent when a network interface is discovered.

```csharp
using Makaretu.Dns;

var mdns = new MulticastService();
mdns.NetworkInterfaceDiscovered += (s, e) => mdns.SendQuery("appletv.local");
mdns.AnswerReceived += (s, e) => { // do something with e.Message };
mdns.Start();
```

# License
Copyright © 2018 Richard Schneider (makaretu@gmail.com)

The package is licensed under the [MIT](http://www.opensource.org/licenses/mit-license.php "Read more about the MIT license form") license. Refer to the [LICENSE](https://github.com/richardschneider/net-mdns/blob/master/LICENSE) file for more information.
