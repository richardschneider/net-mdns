using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace Makaretu.Dns
{

    [TestClass]
    public class NULLRecordTest
    {
        [TestMethod]
        public void Roundtrip()
        {
            var a = new NULLRecord
            {
                NAME = "emanon.org",
                RDATA = new byte[] { 1, 2, 3, 4}
            };
            var b = (NULLRecord)new ResourceRecord().Read(a.ToByteArray());
            Assert.AreEqual(a.NAME, b.NAME);
            Assert.AreEqual(a.CLASS, b.CLASS);
            Assert.AreEqual(a.TYPE, b.TYPE);
            Assert.AreEqual(a.TTL, b.TTL);
            CollectionAssert.AreEqual(a.RDATA, b.RDATA);
        }
    }
}
