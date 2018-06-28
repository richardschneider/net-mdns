using Makaretu.Dns;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;

namespace Makaretu.Dns
{
    
    [TestClass]
    public class MulticastServiceTest
    {
        [TestMethod]
        public void Can_Create()
        {
            var mdns = new MulticastService();
            Assert.IsNotNull(mdns);
        }

        [TestMethod]
        public void StartStop()
        {
            var mdns = new MulticastService();
            mdns.Start();
            mdns.Stop();
        }

        [TestMethod]
        public void SendQuery()
        {
            var ready = new ManualResetEvent(false);
            var done = new ManualResetEvent(false);
            Message msg = null;

            var mdns = new MulticastService();
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
        public void SendNonQuery()
        {
            var query = new Message
            {
                Opcode = MessageOperation.Status,
                QR = false
            };
            var done = new ManualResetEvent(false);
 
            var mdns = new MulticastService();
            mdns.NetworkInterfaceDiscovered += (s, e) => mdns.SendQuery(query);
            mdns.QueryReceived += (s, e) => done.Set();
            try
            {
                mdns.Start();
                Assert.IsFalse(done.WaitOne(TimeSpan.FromSeconds(0.5)), "query was not ignored");
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

            var mdns = new MulticastService();
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
                Assert.AreEqual(MessageStatus.NoError, response.Status);
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
        public void ReceiveErrorAnswer()
        {
            var service = Guid.NewGuid().ToString() + ".local";
            var done = new ManualResetEvent(false);

            var mdns = new MulticastService();
            mdns.NetworkInterfaceDiscovered += (s, e) => mdns.SendQuery(service);
            mdns.QueryReceived += (s, e) =>
            {
                var msg = e.Message;
                if (msg.Questions.Any(q => q.Name == service))
                {
                    var res = msg.CreateResponse();
                    res.Status = MessageStatus.Refused;
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
                    done.Set();
                }
            };
            try
            {
                mdns.Start();
                Assert.IsFalse(done.WaitOne(TimeSpan.FromSeconds(0.5)), "answer was not ignored");
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
            var mdns = new MulticastService();
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

        [TestMethod]
        public void SendQuery_TooBig()
        {
            var done = new ManualResetEvent(false);
            var mdns = new MulticastService();
            mdns.NetworkInterfaceDiscovered += (s, e) => done.Set();
            mdns.Start();
            try
            {
                Assert.IsTrue(done.WaitOne(TimeSpan.FromSeconds(1)), "no nic");
                var query = new Message();
                query.Questions.Add(new Question { Name = "foo.bar.org" });
                query.AdditionalRecords.Add(new NULLRecord { Name = "foo.bar.org", Data = new byte[9000] });
                ExceptionAssert.Throws<ArgumentOutOfRangeException>(() => {
                    mdns.SendQuery(query);
                });
            }
            finally
            {
                mdns.Stop();
            }
        }

        [TestMethod]
        public void SendAnswer_TooBig()
        {
            var done = new ManualResetEvent(false);
            var mdns = new MulticastService();
            mdns.NetworkInterfaceDiscovered += (s, e) => done.Set();
            mdns.Start();
            try
            {
                Assert.IsTrue(done.WaitOne(TimeSpan.FromSeconds(1)), "no nic");
                var answer = new Message();
                answer.Answers.Add(new ARecord { Name = "foo.bar.org", Address = IPAddress.Loopback });
                answer.AdditionalRecords.Add(new NULLRecord { Name = "foo.bar.org", Data = new byte[9000] });
                ExceptionAssert.Throws<ArgumentOutOfRangeException>(() => {
                    mdns.SendAnswer(answer);
                });
            }
            finally
            {
                mdns.Stop();
            }
        }

        [TestMethod]
        public void Multiple_Services()
        {
            var service = Guid.NewGuid().ToString() + ".local";
            var done = new ManualResetEvent(false);
            Message response = null;

            var a = new MulticastService();
            a.QueryReceived += (s, e) =>
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
                    a.SendAnswer(res);
                }
            };

            var b = new MulticastService();
            b.NetworkInterfaceDiscovered += (s, e) => b.SendQuery(service);
            b.AnswerReceived += (s, e) =>
            {
                var msg = e.Message;
                if (msg.Answers.Any(ans => ans.Name == service))
                {
                    response = msg;
                    done.Set();
                }
            };
            try
            {
                a.Start();
                b.Start();
                Assert.IsTrue(done.WaitOne(TimeSpan.FromSeconds(1)), "answer timeout");
                Assert.IsNotNull(response);
                Assert.IsTrue(response.IsResponse);
                Assert.AreEqual(MessageStatus.NoError, response.Status);
                Assert.IsTrue(response.AA);
                var answer = (ARecord)response.Answers[0];
                Assert.AreEqual(IPAddress.Parse("127.1.1.1"), answer.Address);
            }
            finally
            {
                b.Stop();
                a.Stop();
            }
        }

    }
}
