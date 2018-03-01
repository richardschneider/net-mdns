using Makaretu.Dns;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
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
                Assert.AreEqual("some-service.local", msg.Questions.First().Name);
                Assert.AreEqual(Class.IN, msg.Questions.First().Class);
            }
            finally
            {
                mdns.Stop();
            }
        }

        [TestMethod]
        public void ReceiveAnswer()
        {
            var service = Guid.NewGuid().ToString() + ".local";
            var done = new ManualResetEvent(false);
            Message response = null;

            var mdns = new MdnsService();
            mdns.NetworkInterfaceDiscovered += (s, e) => mdns.SendQuery(service);
            mdns.QueryReceived += (s, e) =>
            {
                var msg = e.Message;
                if (msg.Questions.Any(q => q.Name == service))
                {
                    var res = msg.CreateResponse();
                    res.Answers.Add(new ARecord
                    {
                        Name = service,
                        Address = IPAddress.Parse("127.1.1.1")
                    });
                    mdns.SendAnswer(res);
                }
            };
            mdns.AnswerReceived += (s, e) =>
            {
                var msg = e.Message;
                if (msg.Answers.Any(a => a.Name == service))
                {
                    response = msg;
                    done.Set();
                }
            };
            try
            {
                mdns.Start();
                Assert.IsTrue(done.WaitOne(TimeSpan.FromSeconds(1)), "answer timeout");
                Assert.IsNotNull(response);
                Assert.IsTrue(response.IsResponse);
                Assert.AreEqual(Message.Rcode.NoError, response.RCODE);
                Assert.IsTrue(response.AA);
                var a = (ARecord)response.Answers[0];
                Assert.AreEqual(IPAddress.Parse("127.1.1.1"), a.Address);
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
