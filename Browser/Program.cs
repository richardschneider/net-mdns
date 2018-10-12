using Makaretu.Dns;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Browser
{
    class Program
    {
        public static void Main(string[] args)
        {
            var mdns = new MulticastService();
            var sd = new ServiceDiscovery(mdns);

            mdns.NetworkInterfaceDiscovered += (s, e) =>
            {
                foreach (var nic in e.NetworkInterfaces)
                {
                    Console.WriteLine($"NIC '{nic.Name}'");
                }

                // Ask for the name of all services.
                sd.QueryAllServices();
            };

            sd.ServiceDiscovered += (s, serviceName) =>
            {
                Console.WriteLine($"service '{serviceName}'");

                // Ask for the name of instances of the service.
                mdns.SendQuery(serviceName, type: DnsType.PTR);
            };

            sd.ServiceInstanceDiscovered += (s, e) =>
            {
                Console.WriteLine($"service instance '{e.ServiceInstanceName}'");

                // Ask for the service instance details.
                mdns.SendQuery(e.ServiceInstanceName, type: DnsType.SRV);
            };

            mdns.AnswerReceived += (s, e) =>
            {
                // Is this an answer to a service instance details?
                var servers = e.Message.Answers.OfType<SRVRecord>();
                foreach (var server in servers)
                {
                    Console.WriteLine($"host '{server.Target}' for '{server.Name}'");

                    // Ask for the host IP addresses.
                    mdns.SendQuery(server.Target, type: DnsType.A);
                    mdns.SendQuery(server.Target, type: DnsType.AAAA);
                }

                // Is this an answer to host addresses?
                var addresses = e.Message.Answers.OfType<AddressRecord>();
                foreach (var address in addresses)
                {
                    Console.WriteLine($"host '{address.Name}' at {address.Address}");
                }

            };

            try
            {
                mdns.Start();
                Console.ReadKey();
            }
            finally
            {
                sd.Dispose();
                mdns.Stop();
            }
        }
    }
}