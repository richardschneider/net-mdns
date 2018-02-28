using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;

namespace Makaretu.Dns
{

    [TestClass]
    public class ReaderWriterTest
    {
        [TestMethod]
        public void Roundtrip()
        {
            var someBytes = new byte[] { 1, 2, 3 };
            var ms = new MemoryStream();
            var writer = new DnsWriter(ms);
            writer.WriteDomainName("emanon.org");
            writer.WriteString("alpha");
            writer.WriteTimeSpan(TimeSpan.FromHours(3));
            writer.WriteUInt16(ushort.MaxValue);
            writer.WriteUInt32(uint.MaxValue);
            writer.WriteBytes(someBytes);
            writer.WriteIPAddress(IPAddress.Parse("127.0.0.1"));
            writer.WriteIPAddress(IPAddress.Parse("2406:e001:13c7:1:7173:ef8:852f:25cb"));

            ms.Position = 0;
            var reader = new DnsReader(ms);
            Assert.AreEqual("emanon.org", reader.ReadDomainName());
            Assert.AreEqual("alpha", reader.ReadString());
            Assert.AreEqual(TimeSpan.FromHours(3), reader.ReadTimeSpan());
            Assert.AreEqual(ushort.MaxValue, reader.ReadUInt16());
            Assert.AreEqual(uint.MaxValue, reader.ReadUInt32());
            CollectionAssert.AreEqual(someBytes, reader.ReadBytes(3));
            Assert.AreEqual(IPAddress.Parse("127.0.0.1"), reader.ReadIPAddress());
            Assert.AreEqual(IPAddress.Parse("2406:e001:13c7:1:7173:ef8:852f:25cb"), reader.ReadIPAddress(16));
        }

        [TestMethod]
        public void BufferOverflow_Byte()
        {
            var ms = new MemoryStream(new byte[0]);
            var reader = new DnsReader(ms);
            ExceptionAssert.Throws<EndOfStreamException>(() => reader.ReadByte());
        }

        [TestMethod]
        public void BufferOverflow_Bytes()
        {
            var ms = new MemoryStream(new byte[] { 1, 2 });
            var reader = new DnsReader(ms);
            ExceptionAssert.Throws<EndOfStreamException>(() => reader.ReadBytes(3));
        }

        [TestMethod]
        public void BufferOverflow_DomainName()
        {
            var ms = new MemoryStream(new byte[] { 1, (byte)'a' });
            var reader = new DnsReader(ms);
            ExceptionAssert.Throws<EndOfStreamException>(() => reader.ReadDomainName());
        }

        [TestMethod]
        public void BufferOverflow_String()
        {
            var ms = new MemoryStream(new byte[] { 10, 1 });
            var reader = new DnsReader(ms);
            ExceptionAssert.Throws<EndOfStreamException>(() => reader.ReadString());
        }

        [TestMethod]
        public void LengthPrefixedScope()
        {
            var ms = new MemoryStream();
            var writer = new DnsWriter(ms);
            writer.WriteString("abc");
            writer.PushLengthPrefixedScope();
            writer.WriteDomainName("a");
            writer.WriteDomainName("a");
            writer.PopLengthPrefixedScope();

            ms.Position = 0;
            var reader = new DnsReader(ms);
            Assert.AreEqual("abc", reader.ReadString());
            Assert.AreEqual(5, reader.ReadUInt16());
            Assert.AreEqual("a", reader.ReadDomainName());
            Assert.AreEqual("a", reader.ReadDomainName());
        }
    }
}
