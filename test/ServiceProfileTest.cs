﻿using System;
using System.Linq;
using System.Net;
using Microsoft.VisualStudio.TestTools.UnitTesting;

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
            var service = new ServiceProfile("x", "_sdtest._udp", 1024, new[] { IPAddress.Loopback });

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

        [TestMethod]
        public void TXTRecords()
        {
            var service = new ServiceProfile("x", "_sdtest._udp", 1024);
            var txt = service.Resources.OfType<TXTRecord>().First();
            txt.Strings.AddRange(new[] { "a=1", "b=2" });
            CollectionAssert.Contains(txt.Strings, "txtvers=1");
            CollectionAssert.Contains(txt.Strings, "a=1");
            CollectionAssert.Contains(txt.Strings, "b=2");
        }

        [TestMethod]
        public void AddProperty()
        {
            var service = new ServiceProfile
            {
                InstanceName = "x",
                ServiceName = "_sdtest._udp"
            };
            service.AddProperty("a", "1");

            var txt = service.Resources.OfType<TXTRecord>().First();
            Assert.AreEqual(service.FullyQualifiedName, txt.Name);
            CollectionAssert.Contains(txt.Strings, "a=1");
        }

        [TestMethod]
        public void TTLs()
        {
            var service = new ServiceProfile("x", "_sdtest._udp", 1024);
            Assert.AreEqual(TimeSpan.FromMinutes(75), service.Resources.OfType<TXTRecord>().First().TTL);
            Assert.AreEqual(TimeSpan.FromSeconds(120), service.Resources.OfType<AddressRecord>().First().TTL);
        }
    }
}
