﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

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

            using (var mdns = new MulticastService())
            {
                mdns.NetworkInterfaceDiscovered += (s, e) => ready.Set();

                mdns.QueryReceived += (s, e) =>
                {
                    msg = e.Message;
                    done.Set();
                };

                mdns.Start();

                Assert.IsTrue(ready.WaitOne(TimeSpan.FromSeconds(1)), "ready timeout");

                mdns.SendQuery("some-service.local");

                Assert.IsTrue(done.WaitOne(TimeSpan.FromSeconds(1)), "query timeout");
                Assert.AreEqual("some-service.local", msg.Questions.First().Name);
                Assert.AreEqual(DnsClass.IN, msg.Questions.First().Class);
            }
        }

        [TestMethod]
        public void ReceiveAnswer()
        {
            var service = Guid.NewGuid().ToString() + ".local";
            var done = new ManualResetEvent(false);
            Message response = null;

            using (var mdns = new MulticastService())
            {
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
                    if (msg.Answers.Any(answer => answer.Name == service))
                    {
                        response = msg;
                        done.Set();
                    }
                };

                mdns.Start();

                Assert.IsTrue(done.WaitOne(TimeSpan.FromSeconds(1)), "answer timeout");
                Assert.IsNotNull(response);
                Assert.IsTrue(response.IsResponse);
                Assert.AreEqual(MessageStatus.NoError, response.Status);
                Assert.IsTrue(response.AA);

                var a = (ARecord)response.Answers[0];
                Assert.AreEqual(IPAddress.Parse("127.1.1.1"), a.Address);
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
            using (var mdns = new MulticastService())
            {
                mdns.NetworkInterfaceDiscovered += (s, e) => done.Set();

                mdns.Start();

                Assert.IsTrue(done.WaitOne(TimeSpan.FromSeconds(1)), "no nic");

                var query = new Message();
                query.Questions.Add(new Question { Name = "foo.bar.org" });
                query.AdditionalRecords.Add(new NULLRecord { Name = "foo.bar.org", Data = new byte[9000] });

                ExceptionAssert.Throws<ArgumentOutOfRangeException>(() =>
                {
                    mdns.SendQuery(query);
                });
            }
        }

        [TestMethod]
        public void SendAnswer_TooBig()
        {
            var done = new ManualResetEvent(false);
            using (var mdns = new MulticastService())
            {
                mdns.NetworkInterfaceDiscovered += (s, e) => done.Set();

                mdns.Start();

                Assert.IsTrue(done.WaitOne(TimeSpan.FromSeconds(1)), "no nic");

                var answer = new Message();
                answer.Answers.Add(new ARecord { Name = "foo.bar.org", Address = IPAddress.Loopback });
                answer.Answers.Add(new NULLRecord { Name = "foo.bar.org", Data = new byte[9000] });

                ExceptionAssert.Throws<ArgumentOutOfRangeException>(() =>
                {
                    mdns.SendAnswer(answer);
                });
            }
        }

        [TestMethod]
        public void Multiple_Services()
        {
            var service = Guid.NewGuid().ToString() + ".local";
            var done = new ManualResetEvent(false);
            Message response = null;

            using (var a = new MulticastService())
            using (var b = new MulticastService())
            {
                a.QueryReceived += (s, e) =>
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
                        a.SendAnswer(res);
                    }
                };

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

                a.Start();
                b.Start();

                Assert.IsTrue(done.WaitOne(TimeSpan.FromSeconds(1)), "answer timeout");
                Assert.IsNotNull(response);
                Assert.IsTrue(response.IsResponse);
                Assert.AreEqual(MessageStatus.NoError, response.Status);
                Assert.IsTrue(response.AA);
                Assert.AreNotEqual(0, response.Answers.Count);
            }
        }

        [TestMethod]
        public void IPAddresses()
        {
            var addresses = MulticastService.GetIPAddresses().ToArray();
            Assert.AreNotEqual(0, addresses.Length);
        }

        [TestMethod]
        public void Disposable()
        {
            using (var mdns = new MulticastService())
            {
                Assert.IsNotNull(mdns);
            }

            using (var mdns = new MulticastService())
            {
                Assert.IsNotNull(mdns);
                mdns.Start();
            }
        }

        [TestMethod]
        public async Task Resolve()
        {
            var service = Guid.NewGuid().ToString() + ".local";
            var query = new Message();
            query.Questions.Add(new Question { Name = service, Type = DnsType.ANY });
            var cancellation = new CancellationTokenSource(2000);

            using (var mdns = new MulticastService())
            {
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

                mdns.Start();

                var response = await mdns.ResolveAsync(query, cancellation.Token);
                Assert.IsNotNull(response, "no response");
                Assert.IsTrue(response.IsResponse);
                Assert.AreEqual(MessageStatus.NoError, response.Status);
                Assert.IsTrue(response.AA);

                var a = (ARecord)response.Answers[0];
                Assert.AreEqual(IPAddress.Parse("127.1.1.1"), a.Address);
            }
        }

        [TestMethod]
        public void Resolve_NoAnswer()
        {
            var service = Guid.NewGuid().ToString() + ".local";
            var query = new Message();
            query.Questions.Add(new Question { Name = service, Type = DnsType.ANY });
            var cancellation = new CancellationTokenSource(500);

            using (var mdns = new MulticastService())
            {
                mdns.Start();

                ExceptionAssert.Throws<TaskCanceledException>(() =>
                {
                    var _ = mdns.ResolveAsync(query, cancellation.Token).Result;
                });
            }
        }

        [TestMethod]
        public void DuplicateResponse()
        {
            var loker = new object();
            var service = Guid.NewGuid().ToString() + ".local";

            using (var mdns = new MulticastService())
            {
                var answerCount = 0;
                var mre = new ManualResetEvent(false);

                mdns.NetworkInterfaceDiscovered += (s, e) =>
                {
                    mdns.SendQuery(service);
                    Thread.Sleep(250);
                    mdns.SendQuery(service);
                };

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
                        mdns.SendAnswer(res, true);
                    }
                };

                mdns.AnswerReceived += (s, e) =>
                {
                    mre.Reset();

                    var msg = e.Message;
                    if (msg.Answers.Any(answer => answer.Name == service))
                    {
                        lock (loker)
                        {
                            ++answerCount;
                        }
                    };
                };

                mdns.Start();

                mre.WaitOne(1000);
                mre.Close();

                Assert.AreEqual(1, answerCount);
            }
        }

        [TestMethod]
        public void NoDuplicateResponse()
        {
            var loker = new object();
            var service = Guid.NewGuid().ToString() + ".local";

            using (var mdns = new MulticastService())
            {
                var answerCount = 0;
                var mre = new ManualResetEvent(false);

                mdns.NetworkInterfaceDiscovered += (s, e) =>
                {
                    mdns.SendQuery(service);
                    Thread.Sleep(250);
                    mdns.SendQuery(service);
                };

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
                        mdns.SendAnswer(res, false);
                    }
                };

                mdns.AnswerReceived += (s, e) =>
                {
                    mre.Reset();
                    var msg = e.Message;
                    if (msg.Answers.Any(answer => answer.Name == service))
                    {
                        lock (loker)
                        {
                            ++answerCount;
                        }
                    };
                };

                mdns.Start();

                mre.WaitOne(1000);
                mre.Close();

                Assert.AreEqual(2, answerCount);
            }
        }

        [TestMethod]
        public void Multiple_Listeners()
        {
            var ready1 = new ManualResetEvent(false);
            var ready2 = new ManualResetEvent(false);
            using (var mdns1 = new MulticastService())
            using (var mdns2 = new MulticastService())
            {
                mdns1.NetworkInterfaceDiscovered += (s, e) => ready1.Set();
                mdns1.Start();

                mdns2.NetworkInterfaceDiscovered += (s, e) => ready2.Set();
                mdns2.Start();

                Assert.IsTrue(ready1.WaitOne(TimeSpan.FromSeconds(1)), "ready1 timeout");
                Assert.IsTrue(ready2.WaitOne(TimeSpan.FromSeconds(1)), "ready2 timeout");
            }
        }
    }
}
