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
                Name = "emanon.org",
                DomainName = "somewhere.else.org"
            };
            var b = (PTRRecord)new ResourceRecord().Read(a.ToByteArray());
            Assert.AreEqual(a.Name, b.Name);
            Assert.AreEqual(a.Class, b.Class);
            Assert.AreEqual(a.Type, b.Type);
            Assert.AreEqual(a.TTL, b.TTL);
            Assert.AreEqual(a.DomainName, b.DomainName);
        }
    }
}
