using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Makaretu.Dns
{

    [TestClass]
    public class NameCompressionTest
    {
        [TestMethod]
        public void Writing()
        {
            var ms = new MemoryStream();
            var writer = new DnsWriter(ms);
            writer.WriteDomainName("a");
            writer.WriteDomainName("b");
            writer.WriteDomainName("b");
            var bytes = ms.ToArray();
            var expected = new byte[]
            {
                0x01, (byte)'a', 0,
                0x01, (byte)'b', 0,
                0XC0, 3
            };
            CollectionAssert.AreEqual(expected, bytes);
        }

        [TestMethod]
        public void Reading()
        {
            var bytes = new byte[]
            {
                0x01, (byte)'a', 0,
                0x01, (byte)'b', 0,
                0XC0, 3
            };
            var ms = new MemoryStream(bytes);
            var reader = new DnsReader(ms);
            Assert.AreEqual("a", reader.ReadDomainName());
            Assert.AreEqual("b", reader.ReadDomainName());
            Assert.AreEqual("b", reader.ReadDomainName());
        }

    }
}
