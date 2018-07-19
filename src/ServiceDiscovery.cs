using Makaretu.Dns.Resolving;
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

        NameServer localDomain = new NameServer {
            Catalog = new Catalog(),
            AnswerAllQuestions = true
        };

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

            var catalog = localDomain.Catalog;
            catalog.Add(
                new PTRRecord { Name = ServiceName, DomainName = service.QualifiedServiceName },
                authoritative: true);
            catalog.Add(
                new PTRRecord { Name = service.QualifiedServiceName, DomainName = service.FullyQualifiedName },
                authoritative: true);

            foreach (var r in service.Resources)
            {
                catalog.Add(r, authoritative: true);
            }
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
            var response = localDomain.ResolveAsync(request).Result;
            if (response.Status == MessageStatus.NoError)
            {
                response.AdditionalRecords.Clear();
                mdns.SendAnswer(response);
                Console.WriteLine($"Response time {(DateTime.Now - request.CreationTime).TotalMilliseconds}ms");
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
                        mdns.Dispose();
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
