using Makaretu.Dns;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;

namespace Makaretu.Mdns
{
    
    [TestClass]
    public class MdnsTest
    {
        [TestMethod]
        public void Can_Create()
        {
            var mdns = new MdnsService();
            Assert.IsNotNull(mdns);
        }

        [TestMethod]
        public void StartStop()
        {
            var mdns = new MdnsService();
            mdns.Start();
            mdns.Stop();
        }

        [TestMethod]
        public void SendQuery()
        {
            var ready = new ManualResetEvent(false);
            var done = new ManualResetEvent(false);
            Message msg = null;

            var mdns = new MdnsService();
            mdns.NetworkInterfaceDiscovered += (s, e) => ready.Set();
            mdns.QueryReceived += (s, e) => 
            {
                msg = e.Message;
                done.Set();
            };
            try
            {
                mdns.Start();
                Assert.IsTrue(ready.WaitOne(TimeSpan.FromSeconds(1)), "ready timeout");
                mdns.SendQuery("some-service.local");
                Assert.IsTrue(done.WaitOne(TimeSpan.FromSeconds(1)), "query timeout");
                Assert.AreEqual("some-service.local", msg.Questions.First().QNAME);
            }
            finally
            {
                mdns.Stop();
            }
        }

        [TestMethod]
        public void Nics()
        {
            var done = new ManualResetEvent(false);
            var mdns = new MdnsService();
            IEnumerable<NetworkInterface> nics = null;
            mdns.NetworkInterfaceDiscovered += (s, e) =>
            {
                nics = e.NetworkInterfaces;
                done.Set();
            };
            mdns.Start();
            try
            {
                Assert.IsTrue(done.WaitOne(TimeSpan.FromSeconds(1)), "timeout");
                Assert.IsTrue(nics.Count() > 0);
            }
            finally
            {
                mdns.Stop();
            }
        }
    }
}
