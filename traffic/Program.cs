using Makaretu.Dns;
using System;

namespace traffic
{
    class Program
    {
        static readonly object ttyLock = new object();

        static void Main(string[] args)
        {
            var mdns = new MulticastService();
            mdns.NetworkInterfaceDiscovered += (s, e)
                => mdns.SendQuery(ServiceDiscovery.ServiceName, type: DnsType.PTR);
            mdns.AnswerReceived += OnGoodDnsMessage;
            mdns.QueryReceived += OnGoodDnsMessage;
            mdns.MalformedMessage += OnBadDnsMessage;
            mdns.Start();
            Console.ReadKey();
        }

        private static void OnBadDnsMessage(object sender, byte[] packet)
        {
            lock (ttyLock)
            {
                Console.WriteLine(">>> {0:O} <<<", DateTime.Now);
                Console.WriteLine("Malformed message (base64)");
                Console.WriteLine(Convert.ToBase64String(packet));
            }

            Environment.Exit(1);
        }

        private static void OnGoodDnsMessage(object sender, MessageEventArgs e)
        {
            lock (ttyLock)
            {
                Console.WriteLine("=== {0:O} ===", DateTime.Now);
                Console.WriteLine(e.Message.ToString());
            }
        }
    }
}
