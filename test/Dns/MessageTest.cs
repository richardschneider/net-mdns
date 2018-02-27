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
        public void Decode1()
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
    }
}
