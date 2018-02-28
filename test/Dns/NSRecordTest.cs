using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace Makaretu.Dns
{

    [TestClass]
    public class NSRecordTest
    {
        [TestMethod]
        public void Roundtrip()
        {
            var a = new NSRecord
            {
                NAME = "emanon.org",
                NSDNAME = "mydomain.name"
            };
            var b = (NSRecord)new ResourceRecord().Read(a.ToByteArray());
            Assert.AreEqual(a.NAME, b.NAME);
            Assert.AreEqual(a.CLASS, b.CLASS);
            Assert.AreEqual(a.TYPE, b.TYPE);
            Assert.AreEqual(a.TTL, b.TTL);
            Assert.AreEqual(a.NSDNAME, b.NSDNAME);
        }
    }
}
