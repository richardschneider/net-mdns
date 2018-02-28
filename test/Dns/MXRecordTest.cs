using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace Makaretu.Dns
{

    [TestClass]
    public class MXRecordTest
    {
        [TestMethod]
        public void Roundtrip()
        {
            var a = new MXRecord
            {
                NAME = "emanon.org",
                PREFERENCE = 10,
                EXCHANGE = "mail.emanon.org"
            };
            var b = (MXRecord)new ResourceRecord().Read(a.ToByteArray());
            Assert.AreEqual(a.NAME, b.NAME);
            Assert.AreEqual(a.CLASS, b.CLASS);
            Assert.AreEqual(a.TYPE, b.TYPE);
            Assert.AreEqual(a.TTL, b.TTL);
            Assert.AreEqual(a.PREFERENCE, b.PREFERENCE);
            Assert.AreEqual(a.EXCHANGE, b.EXCHANGE);
        }
    }
}
