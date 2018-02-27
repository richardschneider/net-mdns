using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Makaretu.Dns
{

    [TestClass]
    public class MessageTest
    {
        /// <summary>
        ///   From https://en.wikipedia.org/wiki/Multicast_DNS
        /// </summary>
        [TestMethod]
        public void DecodeQuery()
        {
            var bytes = new byte[]
            {
                0x00, 0x00,             // Transaction ID
                0x00, 0x00,             // Flags
                0x00, 0x01,             // Number of questions
                0x00, 0x00,             // Number of answers
                0x00, 0x00,             // Number of authority resource records
                0x00, 0x00,             // Number of additional resource records
                0x07, 0x61, 0x70, 0x70, 0x6c, 0x65, 0x74, 0x76, // "appletv"
                0x05, 0x6c, 0x6f, 0x63, 0x61, 0x6c, // "local"
                0x00,                   // Terminator
                0x00, 0x01,             // Type (A record)
                0x00, 0x01              // Class
            };
            var msg = new Message();
            msg.Read(bytes, 0, bytes.Length);
            Assert.AreEqual(0, msg.ID);
            Assert.AreEqual(1, msg.Questions.Count);
            Assert.AreEqual(0, msg.Answers.Count);
            Assert.AreEqual(0, msg.AuthorityRecords.Count);
            Assert.AreEqual(0, msg.AdditionalRecords.Count);
            var question = msg.Questions.First();
            Assert.AreEqual("appletv.local", question.QNAME);
            Assert.AreEqual(1, question.QTYPE);
            Assert.AreEqual(CLASS.IN, question.QCLASS);
        }

        /// <summary>
        ///   From https://en.wikipedia.org/wiki/Multicast_DNS
        /// </summary>
        [TestMethod]
        public void DecodeResponse()
        {
            var bytes = new byte[]
            {
                0x00, 0x00, 0x84, 0x00, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x02, 0x07, 0x61, 0x70, 0x70,
                0x6c, 0x65, 0x74, 0x76, 0x05, 0x6c, 0x6f, 0x63, 0x61, 0x6c, 0x00, 0x00, 0x01, 0x80, 0x01, 0x00,
                0x00, 0x78, 0x00, 0x00, 0x04, 0x99, 0x6d, 0x07, 0x5a, 0xc0, 0x0c, 0x00, 0x1c, 0x80, 0x01, 0x00,
                0x00, 0x78, 0x00, 0x00, 0x10, 0xfe, 0x80, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x02, 0x23, 0x32,
                0xff, 0xfe, 0xb1, 0x21, 0x52, 0xc0, 0x0c, 0x00, 0x2f, 0x80, 0x01, 0x00, 0x00, 0x78, 0x00, 0x00,
                0x08, 0xc0, 0x0c, 0x00, 0x04, 0x40, 0x00, 0x00, 0x08
            };
            var msg = new Message();
            msg.Read(bytes, 0, bytes.Length);

            Assert.AreEqual(0, msg.Questions.Count);
            Assert.AreEqual(1, msg.Answers.Count);
            Assert.AreEqual(0, msg.AuthorityRecords.Count);
            Assert.AreEqual(2, msg.AdditionalRecords.Count);

            Assert.AreEqual("appletv.local", msg.Answers[0].NAME);
            Assert.AreEqual(1, msg.Answers[0].TYPE);
            Assert.AreEqual(0x8001, msg.Answers[0].CLASS);
            Assert.AreEqual(TimeSpan.FromSeconds(30720), msg.Answers[0].TTL);
        }
    }
}
