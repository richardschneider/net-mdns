using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace Makaretu.Dns
{

    [TestClass]
    public class SOARecordTest
    {
        [TestMethod]
        public void Roundtrip()
        {
            var a = new SOARecord
            {
                Name = "owner-name",
                PrimaryName = "emanaon.org",
                Mailbox = "hostmaster.emanon.org",
                SerialNumber = 1,
                Refresh = TimeSpan.FromDays(1),
                Retry = TimeSpan.FromMinutes(20),
                Expire = TimeSpan.FromDays(7 * 3),
                Minimum = TimeSpan.FromHours(2)
            };
            var b = (SOARecord)new ResourceRecord().Read(a.ToByteArray());
            Assert.AreEqual(a.Name, b.Name);
            Assert.AreEqual(a.Class, b.Class);
            Assert.AreEqual(a.Type, b.Type);
            Assert.AreEqual(a.TTL, b.TTL);
            Assert.AreEqual(a.PrimaryName, b.PrimaryName);
            Assert.AreEqual(a.Mailbox, b.Mailbox);
            Assert.AreEqual(a.SerialNumber, b.SerialNumber);
            Assert.AreEqual(a.Retry, b.Retry);
            Assert.AreEqual(a.Expire, b.Expire);
            Assert.AreEqual(a.Refresh, b.Refresh);
            Assert.AreEqual(a.Minimum, b.Minimum);
        }
    }
}
