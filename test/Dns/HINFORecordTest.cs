using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace Makaretu.Dns
{

    [TestClass]
    public class HINFORecordTest
    {
        [TestMethod]
        public void Roundtrip()
        {
            var a = new HINFORecord
            {
                NAME = "emanaon.org",
                CPU = "DEC-2020",
                OS = "TOPS20"
            };
            var b = (HINFORecord)new ResourceRecord().Read(a.ToByteArray());
            Assert.AreEqual(a.NAME, b.NAME);
            Assert.AreEqual(a.CLASS, b.CLASS);
            Assert.AreEqual(a.TYPE, b.TYPE);
            Assert.AreEqual(a.TTL, b.TTL);
            Assert.AreEqual(a.CPU, b.CPU);
            Assert.AreEqual(a.OS, b.OS);
        }
    }
}
