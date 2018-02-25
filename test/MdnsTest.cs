using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Makaretu.Mdns
{
    
    [TestClass]
    public class MdnsTest
    {
        [TestMethod]
        public void Can_Create()
        {
            var mdns = new MdnsService();
            Assert.IsNotNull(mdns);
        }

        [TestMethod]
        public void StartStop()
        {
            var mdns = new MdnsService();
            mdns.Start();
            Thread.Sleep(1000);
            mdns.Stop();
            Thread.Sleep(1000);
        }

        [TestMethod]
        public void SendQuery()
        {
            var mdns = new MdnsService();
            mdns.Start();
            Thread.Sleep(10);
            mdns.SendQuery("some-service.local");
            Thread.Sleep(1000);
            mdns.Stop();
            Thread.Sleep(1000);
        }
    }
}
