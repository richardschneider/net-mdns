using Makaretu.Dns;
using System;

namespace Spike
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");

            var mdns = new MulticastService();
            mdns.QueryReceived += (s, e) =>
            {
                Console.WriteLine("got a query");
            };
            mdns.AnswerReceived += (s, e) =>
            {
                Console.WriteLine("got an answer");
            };
            mdns.NetworkInterfaceDiscovered += (s, e) =>
            {
                mdns.SendQuery("ipfs.local");
            };
            mdns.Start();

            Console.ReadKey();
        }
    }
}
