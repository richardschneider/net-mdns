using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace Makaretu.Dns
{
    public class ServiceDiscovery
    {
        /// <summary>
        ///   The service discovery service name.
        /// </summary>
        /// <value>
        ///   The service name used to enumerate other services.
        /// </value>
        public const string ServiceName = "_services._dns-sd._udp.local";

        MulticastService mdns;
        List<ServiceProfile> profiles = new List<ServiceProfile>();

        public ServiceDiscovery()
            : this(new MulticastService())
        {
            // Auto start.
            mdns.Start();
        }

        public ServiceDiscovery(MulticastService mdns)
        {
            this.mdns = mdns;
            mdns.QueryReceived += OnQuery;
            mdns.AnswerReceived += OnAnswer;
        }

        /// <summary>
        ///   Advertise a service profile.
        /// </summary>
        /// <param name="service">
        ///   The service profile.
        /// </param>
        /// <remarks>
        /// </remarks>
        public void Advertise(ServiceProfile service)
        {
            profiles.Add(service);
        }

        private void OnAnswer(object sender, MessageEventArgs e)
        {
            var msg = e.Message;

            // The answer must contain a PTR
            var pointers = msg.Answers.OfType<PTRRecord>();

            foreach (var ptr in pointers)
            {
                Console.WriteLine($"name='{ptr.Name}' domain='{ptr.DomainName}'");
            }

        }

        void OnQuery(object sender, MessageEventArgs e)
        {
            var request = e.Message;
            var response = request.CreateResponse();

            // If a SD meta-query, then respond with all advertised service names.
            if (request.Questions.Any(q => DnsObject.NamesEquals(q.Name, ServiceName) && q.Type == DnsType.PTR))
            {
                var ptrs = profiles
                    .Select(p => p.QualifiedServiceName)
                    .Distinct()
                    .Select(s => new PTRRecord { Name = ServiceName, DomainName = s });
                response.Answers.AddRange(ptrs);
            }

            // If a query for a service, then respond with a PTR to server.
            var servicePtrs = request.Questions
                .Where(q => q.Type == DnsType.PTR || q.Type == DnsType.ANY)
                .SelectMany(q => profiles.Where(p => DnsObject.NamesEquals(q.Name, p.QualifiedServiceName)))
                .Select(p => new PTRRecord { Name = p.QualifiedServiceName, DomainName = p.FullyQualifiedName });
            response.Answers.AddRange(servicePtrs);

            // If a query for the service instance, the respond with all details.
            var resources = request.Questions
                .SelectMany(q => profiles.Where(p => DnsObject.NamesEquals(q.Name, p.FullyQualifiedName)))
                .SelectMany(p => p.Resources);
            response.Answers.AddRange(resources);

            if (response.Answers.Count > 0)
            {
                mdns.SendAnswer(response);
            }
        }
    }

}
