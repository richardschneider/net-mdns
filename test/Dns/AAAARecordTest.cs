using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace Makaretu.Dns
{

    [TestClass]
    public class AAAARecordTest
    {
        [TestMethod]
        public void Roundtrip()
        {
            var a = new AAAARecord
            {
                NAME = "emanon.org",
                ADDRESS = IPAddress.Parse("2406:e001:13c7:1:7173:ef8:852f:25cb")
            };
            var b = (AAAARecord)new ResourceRecord().Read(a.ToByteArray());
            Assert.AreEqual(a.NAME, b.NAME);
            Assert.AreEqual(a.CLASS, b.CLASS);
            Assert.AreEqual(a.TYPE, b.TYPE);
            Assert.AreEqual(a.TTL, b.TTL);
            Assert.AreEqual(a.ADDRESS, b.ADDRESS);
        }
    }
}
