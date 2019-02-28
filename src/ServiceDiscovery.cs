using System;
using System.Collections.Generic;
using System.Linq;
using Common.Logging;
using Makaretu.Dns.Resolving;

namespace Makaretu.Dns
{
    /// <summary>
    ///   DNS based Service Discovery is a way of using standard DNS programming interfaces, servers, 
    ///   and packet formats to browse the network for services.
    /// </summary>
    /// <seealso href="https://tools.ietf.org/html/rfc6763">RFC 6763 DNS-Based Service Discovery</seealso>
    public class ServiceDiscovery : IDisposable
    {
        static readonly ILog log = LogManager.GetLogger(typeof(ServiceDiscovery));

        /// <summary>
        ///   The service discovery service name.
        /// </summary>
        /// <value>
        ///   The service name used to enumerate other services.
        /// </value>
        public const string ServiceName = "_services._dns-sd._udp.local";
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
            Mdns.Start();
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
            this.Mdns = mdns;
            mdns.QueryReceived += OnQuery;
            mdns.AnswerReceived += OnAnswer;
        }

        /// <summary>
        ///   Gets the multicasting service.
        /// </summary>
        /// <value>
        ///   Is used to send and recieve multicast <see cref="Message">DNS messages</see>.
        /// </value>
        public MulticastService Mdns { get; private set; }

        /// <summary>
        ///   Add the additional records into the answers.
        /// </summary>
        /// <value>
        ///   Defaults to <b>false</b>.
        /// </value>
        /// <remarks>
        ///   Some malformed systems, such as js-ipfs and go-ipfs, only examine
        ///   the <see cref="Message.Answers"/> and not the <see cref="Message.AdditionalRecords"/>.
        ///   Setting this to <b>true</b>, will move the additional records
        ///   into the answers.
        ///   <para>
        ///   This never done for DNS-SD answers.
        ///   </para>
        /// </remarks>
        public bool AnswersContainsAdditionalRecords { get; set; }

        /// <summary>
        ///   Gets the name server.
        /// </summary>
        /// <value>
        ///   Is used to answer questions.
        /// </value>
        public NameServer NameServer { get; } = new NameServer
        {
            Catalog = new Catalog(),
            AnswerAllQuestions = true
        };

        /// <summary>
        ///   Raised when a DNS-SD response is received.
        /// </summary>
        /// <value>
        ///   Contains the service name.
        /// </value>
        /// <remarks>
        ///   <b>ServiceDiscovery</b> passively monitors the network for any answers
        ///   to a DNS-SD query. When an anwser is received this event is raised.
        ///   <para>
        ///   Use <see cref="QueryAllServices"/> to initiate a DNS-SD question.
        ///   </para>
        /// </remarks>
        public event EventHandler<string> ServiceDiscovered;

        /// <summary>
        ///   Raised when a servive instance is discovered.
        /// </summary>
        /// <value>
        ///   Contains the service instance name.
        /// </value>
        /// <remarks>
        ///   <b>ServiceDiscovery</b> passively monitors the network for any answers.
        ///   When an answer containing a PTR to a service instance is received 
        ///   this event is raised.
        /// </remarks>
        public event EventHandler<ServiceInstanceDiscoveryEventArgs> ServiceInstanceDiscovered;

        /// <summary>
        ///    Asks other MDNS services to send their service names.
        /// </summary>
        /// <remarks>
        ///   When an answer is received the <see cref="ServiceDiscovered"/> event is raised.
        /// </remarks>
        public void QueryAllServices()
        {
            Mdns.SendQuery(ServiceName, type: DnsType.PTR);
        }

        /// <summary>
        ///    Asks other MDNS services to send their service names;
        ///    accepts unicast and/or broadcast answers.
        /// </summary>
        /// <remarks>
        ///   When an answer is received the <see cref="ServiceDiscovered"/> event is raised.
        /// </remarks>
        public void QueryUnicastAllServices()
        {
            Mdns.SendUnicastQuery(ServiceName, type: DnsType.PTR);
        }

        /// <summary>
        ///   Asks instances of the specified service to send details.
        /// </summary>
        /// <param name="service">
        ///   The service name to query. Typically of the form "_<i>service</i>._tcp".
        /// </param>
        /// <remarks>
        ///   When an answer is received the <see cref="ServiceInstanceDiscovered"/> event is raised.
        /// </remarks>
        /// <seealso cref="ServiceProfile.ServiceName"/>
        public void QueryServiceInstances(string service)
        {
            Mdns.SendQuery(service + ".local", type: DnsType.PTR);
        }

        /// <summary>
        ///   Asks instances of the specified service to send details.
        ///   accepts unicast and/or broadcast answers.
        /// </summary>
        /// <param name="service">
        ///   The service name to query. Typically of the form "_<i>service</i>._tcp".
        /// </param>
        /// <remarks>
        ///   When an answer is received the <see cref="ServiceInstanceDiscovered"/> event is raised.
        /// </remarks>
        /// <seealso cref="ServiceProfile.ServiceName"/>
        public void QueryUnicastServiceInstances(string service)
        {
            Mdns.SendUnicastQuery(service + ".local", type: DnsType.PTR);
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

            var catalog = NameServer.Catalog;
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

            // Any DNS-SD answers?
            var sd = msg.Answers.OfType<PTRRecord>();
            foreach (var ptr in sd)
            {
                if (ptr.Name == ServiceName)
                {
                    ServiceDiscovered?.Invoke(this, ptr.DomainName);
                }
                else
                {
                    var args = new ServiceInstanceDiscoveryEventArgs
                    {
                        ServiceInstanceName = ptr.DomainName,
                        Message = msg
                    };
                    ServiceInstanceDiscovered?.Invoke(this, args);
                }
            }
        }

        void OnQuery(object sender, MessageEventArgs e)
        {
            var request = e.Message;

            if (log.IsDebugEnabled)
            {
                log.Debug($"got query from: {e.RemoteEndPoint},  for {request.Questions[0].Name} {request.Questions[0].Type}");
            }

            // Determine if this query is requesting a unicast response
            // and normalise the Class.
            var QU = false; // unicast query response?
            foreach (var r in request.Questions)
            {
                if (((ushort)r.Class & 0x8000) != 0)
                {
                    QU = true;
                    r.Class = (DnsClass)((ushort)r.Class & 0x7fff);
                }
            }

            var response = NameServer.ResolveAsync(request).Result;

            if (response.Status != MessageStatus.NoError)
            {
                return;
            }

            // Many bonjour browsers don't like DNS-SD response
            // with additional records.
            if (response.Answers.Any(a => a.Name == ServiceName))
            {
                response.AdditionalRecords.Clear();
            }

            if (AnswersContainsAdditionalRecords)
            {
                response.Answers.AddRange(response.AdditionalRecords);
            }

            if (QU)
            {
                // TODO: Send a Unicast response if required.
                Mdns.SendAnswer(response);
            }
            else
            {
                Mdns.SendAnswer(response);
            }

            if (log.IsDebugEnabled)
            {
                log.Debug($"sent answer {response.Answers[0]}");
            }
            //Console.WriteLine($"Response time {(DateTime.Now - request.CreationTime).TotalMilliseconds}ms");
        }

        #region IDisposable Support

        /// <inheritdoc />
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (Mdns != null)
                {
                    Mdns.QueryReceived -= OnQuery;
                    Mdns.AnswerReceived -= OnAnswer;
                    if (ownsMdns)
                    {
                        Mdns.Dispose();
                    }
                    Mdns = null;
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
