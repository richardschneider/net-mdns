using System;
using System.Linq;
using System.Net;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

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
            var service = new ServiceProfile("x", "_sdtest-1._udp", 1024, new[] { IPAddress.Loopback });
            var done = new ManualResetEvent(false);

            var mdns = new MulticastService();
            mdns.NetworkInterfaceDiscovered += (s, e) =>
                mdns.SendQuery(ServiceDiscovery.ServiceName, DnsClass.IN, DnsType.PTR);
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
                mdns.SendQuery(service.QualifiedServiceName, DnsClass.IN, DnsType.PTR);
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
            var service = new ServiceProfile("x2", "_sdtest-1._udp", 1024, new[] { IPAddress.Loopback });
            var done = new ManualResetEvent(false);

            var mdns = new MulticastService();
            mdns.NetworkInterfaceDiscovered += (s, e) =>
                mdns.SendQuery(service.HostName, DnsClass.IN, DnsType.A);
            mdns.AnswerReceived += (s, e) =>
            {
                var msg = e.Message;
                if (msg.Answers.OfType<ARecord>().Any(p => p.Name == service.HostName))
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
        public void Discover_AllServices()
        {
            var service = new ServiceProfile("x", "_sdtest-2._udp", 1024);
            var done = new ManualResetEvent(false);
            var mdns = new MulticastService();
            var sd = new ServiceDiscovery(mdns);

            mdns.NetworkInterfaceDiscovered += (s, e) => sd.QueryAllServices();
            sd.ServiceDiscovered += (s, serviceName) =>
            {
                if (serviceName == service.QualifiedServiceName)
                {
                    done.Set();
                }
            };
            try
            {
                sd.Advertise(service);
                mdns.Start();
                Assert.IsTrue(done.WaitOne(TimeSpan.FromSeconds(1)), "DNS-SD query timeout");
            }
            finally
            {
                sd.Dispose();
                mdns.Stop();
            }
        }

        [TestMethod]
        public void Discover_AllServices_Unicast()
        {
            var service = new ServiceProfile("x", "_sdtest-5._udp", 1024);
            var done = new ManualResetEvent(false);
            var mdns = new MulticastService();
            var sd = new ServiceDiscovery(mdns);

            mdns.NetworkInterfaceDiscovered += (s, e) => sd.QueryUnicastAllServices();
            sd.ServiceDiscovered += (s, serviceName) =>
            {
                if (serviceName == service.QualifiedServiceName)
                {
                    done.Set();
                }
            };
            try
            {
                sd.Advertise(service);
                mdns.Start();
                Assert.IsTrue(done.WaitOne(TimeSpan.FromSeconds(1)), "DNS-SD query timeout");
            }
            finally
            {
                sd.Dispose();
                mdns.Stop();
            }
        }

        [TestMethod]
        public void Discover_ServiceInstance()
        {
            var service = new ServiceProfile("y", "_sdtest-2._udp", 1024);
            var done = new ManualResetEvent(false);
            var mdns = new MulticastService();
            var sd = new ServiceDiscovery(mdns);

            mdns.NetworkInterfaceDiscovered += (s, e) =>
            {
                sd.QueryServiceInstances(service.ServiceName);
            };

            sd.ServiceInstanceDiscovered += (s, e) =>
            {
                if (e.ServiceInstanceName == service.FullyQualifiedName)
                {
                    Assert.IsNotNull(e.Message);
                    done.Set();
                }
            };
            try
            {
                sd.Advertise(service);
                mdns.Start();
                Assert.IsTrue(done.WaitOne(TimeSpan.FromSeconds(1)), "instance not found");
            }
            finally
            {
                sd.Dispose();
                mdns.Stop();
            }
        }

        [TestMethod]
        public void Discover_ServiceInstance_Unicast()
        {
            var service = new ServiceProfile("y", "_sdtest-5._udp", 1024);
            var done = new ManualResetEvent(false);
            var mdns = new MulticastService();
            var sd = new ServiceDiscovery(mdns);

            mdns.NetworkInterfaceDiscovered += (s, e) =>
            {
                sd.QueryUnicastServiceInstances(service.ServiceName);
            };

            sd.ServiceInstanceDiscovered += (s, e) =>
            {
                if (e.ServiceInstanceName == service.FullyQualifiedName)
                {
                    Assert.IsNotNull(e.Message);
                    done.Set();
                }
            };
            try
            {
                sd.Advertise(service);
                mdns.Start();
                Assert.IsTrue(done.WaitOne(TimeSpan.FromSeconds(1)), "instance not found");
            }
            finally
            {
                sd.Dispose();
                mdns.Stop();
            }
        }

        [TestMethod]
        public void Discover_ServiceInstance_WithAnswersContainingAdditionRecords()
        {
            var service = new ServiceProfile("y", "_sdtest-2._udp", 1024, new[] { IPAddress.Parse("127.1.1.1") });
            var done = new ManualResetEvent(false);

            using (var mdns = new MulticastService())
            using (var sd = new ServiceDiscovery(mdns) { AnswersContainsAdditionalRecords = true })
            {
                Message discovered = null;

                mdns.NetworkInterfaceDiscovered += (s, e) =>
                {
                    sd.QueryServiceInstances(service.ServiceName);
                };

                sd.ServiceInstanceDiscovered += (s, e) =>
                {
                    if (e.ServiceInstanceName == service.FullyQualifiedName)
                    {
                        Assert.IsNotNull(e.Message);
                        discovered = e.Message;
                        done.Set();
                    }
                };

                sd.Advertise(service);

                mdns.Start();

                Assert.IsTrue(done.WaitOne(TimeSpan.FromSeconds(1)), "instance not found");

                var additionalRecordsCount =
                    1 + // SRVRecord
                    1 + // TXTRecord
                    1; // AddressRecord

                var answersCount = additionalRecordsCount +
                    1; // PTRRecord

                Assert.AreEqual(0, discovered.AdditionalRecords.Count);
                Assert.AreEqual(answersCount, discovered.Answers.Count);
            }
        }

        [TestMethod]
        public void Unadvertise()
        {
            var service = new ServiceProfile("z", "_sdtest-7._udp", 1024);
            var done = new ManualResetEvent(false);
            var mdns = new MulticastService();
            var sd = new ServiceDiscovery(mdns);

            mdns.NetworkInterfaceDiscovered += (s, e) => sd.QueryAllServices();
            sd.ServiceInstanceShutdown += (s, e) =>
            {
                if (e.ServiceInstanceName == service.FullyQualifiedName)
                {
                    done.Set();
                }
            };
            try
            {
                sd.Advertise(service);
                mdns.Start();
                sd.Unadvertise(service);
                Assert.IsTrue(done.WaitOne(TimeSpan.FromSeconds(1)), "goodbye timeout");
            }
            finally
            {
                sd.Dispose();
                mdns.Stop();
            }
        }
        [TestMethod]
        public void ReverseAddressMapping()
        {
            var service = new ServiceProfile("x9", "_sdtest-1._udp", 1024, new[] { IPAddress.Loopback, IPAddress.IPv6Loopback });
            var arpaAddress = IPAddress.Loopback.GetArpaName();
            var done = new ManualResetEvent(false);

            var mdns = new MulticastService();
            Message response = null;
            mdns.NetworkInterfaceDiscovered += (s, e) =>
                mdns.SendQuery(arpaAddress, DnsClass.IN, DnsType.PTR);
            mdns.AnswerReceived += (s, e) =>
            {
                var msg = e.Message;
                if (msg.Answers.OfType<PTRRecord>().Any(p => p.Name == arpaAddress))
                {
                    response = msg;
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
                    var answers = response.Answers
                        .OfType<PTRRecord>()
                        .Where(ptr => service.HostName == ptr.DomainName);
                    foreach (var answer in answers)
                    {
                        Assert.AreEqual(arpaAddress, answer.Name);
                        Assert.IsTrue(answer.TTL > TimeSpan.Zero);
                        Assert.AreEqual(DnsClass.IN, answer.Class);
                    }
                }
            }
            finally
            {
                mdns.Stop();
            }
        }

    }
}
