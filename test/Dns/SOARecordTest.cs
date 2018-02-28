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
                NAME = "owner-name",
                MNAME = "emanaon.org",
                RNAME = "hostmaster.emanon.org",
                SERIAL = 1,
                REFRESH = TimeSpan.FromDays(1),
                RETRY = TimeSpan.FromMinutes(20),
                EXPIRE = TimeSpan.FromDays(7 * 3),
                MINIMUM = TimeSpan.FromHours(2)
            };
            var b = (SOARecord)new ResourceRecord().Read(a.ToByteArray());
            Assert.AreEqual(a.NAME, b.NAME);
            Assert.AreEqual(a.CLASS, b.CLASS);
            Assert.AreEqual(a.TYPE, b.TYPE);
            Assert.AreEqual(a.TTL, b.TTL);
            Assert.AreEqual(a.MNAME, b.MNAME);
            Assert.AreEqual(a.RNAME, b.RNAME);
            Assert.AreEqual(a.SERIAL, b.SERIAL);
            Assert.AreEqual(a.RETRY, b.RETRY);
            Assert.AreEqual(a.EXPIRE, b.EXPIRE);
            Assert.AreEqual(a.REFRESH, b.REFRESH);
            Assert.AreEqual(a.MINIMUM, b.MINIMUM);
        }
    }
}
