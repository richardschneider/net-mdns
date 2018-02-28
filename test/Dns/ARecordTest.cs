using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace Makaretu.Dns
{

    [TestClass]
    public class ARecordTest
    {
        [TestMethod]
        public void Roundtrip()
        {
            var a = new ARecord
            {
                NAME = "emanon.org",
                ADDRESS = IPAddress.Parse("127.0.0.1")
            };
            var b = (ARecord)new ResourceRecord().Read(a.ToByteArray());
            Assert.AreEqual(a.NAME, b.NAME);
            Assert.AreEqual(a.CLASS, b.CLASS);
            Assert.AreEqual(a.TYPE, b.TYPE);
            Assert.AreEqual(a.TTL, b.TTL);
            Assert.AreEqual(a.ADDRESS, b.ADDRESS);
        }
    }
}
