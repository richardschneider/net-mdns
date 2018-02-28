using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace Makaretu.Dns
{

    [TestClass]
    public class TXTRecordTest
    {
        [TestMethod]
        public void Roundtrip()
        {
            var a = new TXTRecord
            {
                NAME = "the.printer.local",
                Strings = new List<string>
                {
                    "paper=A4",
                    "colour=false"
                }
            };
            var b = (TXTRecord)new ResourceRecord().Read(a.ToByteArray());
            Assert.AreEqual(a.NAME, b.NAME);
            Assert.AreEqual(a.CLASS, b.CLASS);
            Assert.AreEqual(a.TYPE, b.TYPE);
            Assert.AreEqual(a.TTL, b.TTL);
            CollectionAssert.AreEqual(a.Strings, b.Strings);
        }
    }
}
