using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace Makaretu.Dns
{

    [TestClass]
    public class SRVRecordTest
    {
        [TestMethod]
        public void Roundtrip()
        {
            var a = new SRVRecord
            {
                NAME = "_foobar._tcp",
                Priority = 1,
                Weight = 2,
                Port = 9,
                Target = "foobar.example.com"
            };
            var b = (SRVRecord)new ResourceRecord().Read(a.ToByteArray());
            Assert.AreEqual(a.NAME, b.NAME);
            Assert.AreEqual(a.CLASS, b.CLASS);
            Assert.AreEqual(a.TYPE, b.TYPE);
            Assert.AreEqual(a.TTL, b.TTL);
            Assert.AreEqual(a.Priority, b.Priority);
            Assert.AreEqual(a.Weight, b.Weight);
            Assert.AreEqual(a.Port, b.Port);
            Assert.AreEqual(a.Target, b.Target);
        }
    }
}
