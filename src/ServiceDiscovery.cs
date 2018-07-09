using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace Makaretu.Dns
{
    /// <summary>
    ///   DNS based Service Discovery is a way of using standard DNS programming interfaces, servers, 
    ///   and packet formats to browse the network for services.
    /// </summary>
    public class ServiceDiscovery : IDisposable
    {
        /// <summary>
        ///   The service discovery service name.
        /// </summary>
        /// <value>
        ///   The service name used to enumerate other services.
        /// </value>
        public const string ServiceName = "_services._dns-sd._udp.local";

        MulticastService mdns;
        readonly bool ownsMdns;

        List<ServiceProfile> profiles = new List<ServiceProfile>();

        /// <summary>
        ///   Creates a new instance of the <see cref="ServiceDiscovery"/> class.
        /// </summary>
        public ServiceDiscovery()
            : this(new MulticastService())
        {
            ownsMdns = true;

            // Auto start.
            mdns.Start();
        }

        /// <summary>
        ///   Creates a new instance of the <see cref="ServiceDiscovery"/> class with
        ///   the specified <see cref="MulticastService"/>.
        /// </summary>
        /// <param name="mdns">
        ///   The underlaying <see cref="MulticastService"/> to use.
        /// </param>
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
        ///   Any queries for the service or service instance will be answered with
        ///   information from the profile.
        /// </remarks>
        public void Advertise(ServiceProfile service)
        {
            profiles.Add(service);
        }

        void OnAnswer(object sender, MessageEventArgs e)
        {
            var msg = e.Message;

            // The answer must contain a PTR
            var pointers = msg.Answers.OfType<PTRRecord>();

            // TODO
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

        #region IDisposable Support

        /// <inheritdoc />
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (mdns != null)
                {
                    mdns.QueryReceived -= OnQuery;
                    mdns.AnswerReceived -= OnAnswer;
                    if (ownsMdns)
                    {
                        mdns.Stop();
                    }
                    mdns = null;
                }
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            Dispose(true);
        }
        #endregion
    }

}
