using Makaretu.Dns;
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
    public class ServiveProfileTest
    {
        [TestMethod]
        public void Defaults()
        {
            var service = new ServiceProfile();
            Assert.IsNotNull(service.Resources);
        }

        [TestMethod]
        public void QualifiedNames()
        {
            var service = new ServiceProfile("x", "_sdtest._udp", 1024, new [] { IPAddress.Loopback });

            Assert.AreEqual("_sdtest._udp.local", service.QualifiedServiceName);
            Assert.AreEqual("x._sdtest._udp.local", service.FullyQualifiedName);
        }

        [TestMethod]
        public void ResourceRecords()
        {
            var service = new ServiceProfile("x", "_sdtest._udp", 1024, new[] { IPAddress.Loopback });

            Assert.IsTrue(service.Resources.OfType<SRVRecord>().Any());
            Assert.IsTrue(service.Resources.OfType<TXTRecord>().Any());
            Assert.IsTrue(service.Resources.OfType<ARecord>().Any());
        }

        [TestMethod]
        public void Addresses_Default()
        {
            var service = new ServiceProfile("x", "_sdtest._udp", 1024);
            Assert.IsTrue(service.Resources.Any(r => r.Type == DnsType.A || r.Type == DnsType.AAAA));
        }

        [TestMethod]
        public void Addresses_IPv4()
        {
            var service = new ServiceProfile("x", "_sdtest._udp", 1024, new[] { IPAddress.Loopback });
            Assert.IsTrue(service.Resources.Any(r => r.Type == DnsType.A));
        }

        [TestMethod]
        public void Addresses_IPv6()
        {
            var service = new ServiceProfile("x", "_sdtest._udp", 1024, new[] { IPAddress.IPv6Loopback });
            Assert.IsTrue(service.Resources.Any(r => r.Type == DnsType.AAAA));
        }
    }
}
