using Makaretu.Dns;
using System;
using System.Linq;

namespace Spike
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Multicast DNS spike");

            foreach (var a in MulticastService.GetIPAddresses())
            {
                Console.WriteLine($"IP address {a}");
            }

            var mdns = new MulticastService();
            mdns.QueryReceived += (s, e) =>
            {
                var names = e.Message.Questions
                    .Select(q => q.Name);
                Console.WriteLine($"got a query for {String.Join(", ", names)}");
            };
            mdns.AnswerReceived += (s, e) =>
            {
                var names = e.Message.Answers
                    .Select(q => q.Name)
                    .Distinct();
                Console.WriteLine($"got answer for {String.Join(", ", names)}");
            };
            mdns.NetworkInterfaceDiscovered += (s, e) =>
            {
                foreach (var nic in e.NetworkInterfaces)
                {
                    Console.WriteLine($"discovered NIC '{nic.Name}'");
                }
            };

            var sd = new ServiceDiscovery(mdns);
            sd.Advertise(new ServiceProfile("xyzzy", "_xservice._tcp", 5011));
            sd.Advertise(new ServiceProfile("omega", "_xservice._tcp", 666));
            sd.Advertise(new ServiceProfile("alpha", "_zservice._tcp", 5012));

            mdns.Start();
            Console.ReadKey();
        }
    }
}
