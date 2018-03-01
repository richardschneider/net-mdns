using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace Makaretu.Dns
{

    [TestClass]
    public class UnknownRecordTest
    {
        [TestMethod]
        public void Roundtrip()
        {
            var a = new UnknownRecord
            {
                Name = "emanon.org",
                RDATA = new byte[] { 10, 11, 12  },
            };
            var b = (UnknownRecord)new ResourceRecord().Read(a.ToByteArray());
            Assert.AreEqual(a.Name, b.Name);
            Assert.AreEqual(a.Class, b.Class);
            Assert.AreEqual(a.Type, b.Type);
            Assert.AreEqual(a.TTL, b.TTL);
            CollectionAssert.AreEqual(a.RDATA, b.RDATA);
        }
    }
}
