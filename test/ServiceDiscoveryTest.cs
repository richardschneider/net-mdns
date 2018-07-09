﻿using Makaretu.Dns;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Makaretu.Dns
{
    
    [TestClass]
    public class ServiceDiscoveryTest
    {
        [TestMethod]
        public void Disposable()
        {
            using (var sd = new ServiceDiscovery())
            {
                Assert.IsNotNull(sd);
            }

            var mdns = new MulticastService();
            using (var sd = new ServiceDiscovery(mdns))
            {
                Assert.IsNotNull(sd);
            }
        }

        [TestMethod]
        public void Advertises_Service()
        {
            var service = new ServiceProfile("x", "_sdtest-1._udp", 1024, new [] { IPAddress.Loopback });
            var done = new ManualResetEvent(false);

            var mdns = new MulticastService();
            mdns.NetworkInterfaceDiscovered += (s, e) =>
                mdns.SendQuery(ServiceDiscovery.ServiceName, Class.IN, DnsType.PTR);
            mdns.AnswerReceived += (s, e) =>
            {
                var msg = e.Message;
                if (msg.Answers.OfType<PTRRecord>().Any(p => p.DomainName == service.QualifiedServiceName))
                {
                    done.Set();
                }
            };
            try
            {
                using (var sd = new ServiceDiscovery(mdns))
                {
                    sd.Advertise(service);
                    mdns.Start();                 
                    Assert.IsTrue(done.WaitOne(TimeSpan.FromSeconds(1)), "query timeout");
                }
            }
            finally
            {
                mdns.Stop();
            }
        }

        [TestMethod]
        public void Advertises_ServiceInstances()
        {
            var service = new ServiceProfile("x", "_sdtest-1._udp", 1024, new[] { IPAddress.Loopback });
            var done = new ManualResetEvent(false);

            var mdns = new MulticastService();
            mdns.NetworkInterfaceDiscovered += (s, e) =>
                mdns.SendQuery(service.QualifiedServiceName, Class.IN, DnsType.PTR);
            mdns.AnswerReceived += (s, e) =>
            {
                var msg = e.Message;
                if (msg.Answers.OfType<PTRRecord>().Any(p => p.DomainName == service.FullyQualifiedName))
                {
                    done.Set();
                }
            };
            try
            {
                using (var sd = new ServiceDiscovery(mdns))
                {
                    sd.Advertise(service);
                    mdns.Start();
                    Assert.IsTrue(done.WaitOne(TimeSpan.FromSeconds(1)), "query timeout");
                }
            }
            finally
            {
                mdns.Stop();
            }
        }

        [TestMethod]
        public void Advertises_ServiceInstance_Address()
        {
            var service = new ServiceProfile("x", "_sdtest-1._udp", 1024, new[] { IPAddress.Loopback });
            var done = new ManualResetEvent(false);

            var mdns = new MulticastService();
            mdns.NetworkInterfaceDiscovered += (s, e) =>
                mdns.SendQuery(service.FullyQualifiedName, Class.IN, DnsType.A);
            mdns.AnswerReceived += (s, e) =>
            {
                var msg = e.Message;
                if (msg.Answers.OfType<ARecord>().Any(p => p.Name == service.FullyQualifiedName))
                {
                    done.Set();
                }
            };
            try
            {
                using (var sd = new ServiceDiscovery(mdns))
                {
                    sd.Advertise(service);
                    mdns.Start();
                    Assert.IsTrue(done.WaitOne(TimeSpan.FromSeconds(1)), "query timeout");
                }
            }
            finally
            {
                mdns.Stop();
            }
        }

    }
}
