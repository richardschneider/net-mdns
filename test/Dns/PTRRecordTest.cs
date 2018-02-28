using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace Makaretu.Dns
{

    [TestClass]
    public class PTRRecordTest
    {
        [TestMethod]
        public void Roundtrip()
        {
            var a = new PTRRecord
            {
                NAME = "emanon.org",
                PTRDNAME = "somewhere.else.org"
            };
            var b = (PTRRecord)new ResourceRecord().Read(a.ToByteArray());
            Assert.AreEqual(a.NAME, b.NAME);
            Assert.AreEqual(a.CLASS, b.CLASS);
            Assert.AreEqual(a.TYPE, b.TYPE);
            Assert.AreEqual(a.TTL, b.TTL);
            Assert.AreEqual(a.PTRDNAME, b.PTRDNAME);
        }
    }
}
