using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
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
        static readonly DomainName LocalDomain = new DomainName("local");
        static readonly DomainName SubName = new DomainName("_sub");

        /// <summary>
        ///   The service discovery service name.
        /// </summary>
        /// <value>
        ///   The service name used to enumerate other services.
        /// </value>
        public static readonly DomainName ServiceName = new DomainName("_services._dns-sd._udp.local");

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
        public event EventHandler<DomainName> ServiceDiscovered;

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
        ///   Raised when a servive instance is shutting down.
        /// </summary>
        /// <value>
        ///   Contains the service instance name.
        /// </value>
        /// <remarks>
        ///   <b>ServiceDiscovery</b> passively monitors the network for any answers.
        ///   When an answer containing a PTR to a service instance with a
        ///   TTL of zero is received this event is raised.
        /// </remarks>
        public event EventHandler<ServiceInstanceShutdownEventArgs> ServiceInstanceShutdown;
        
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
        public void QueryServiceInstances(DomainName service)
        {
            Mdns.SendQuery(DomainName.Join(service, LocalDomain), type: DnsType.PTR);
        }

        /// <summary>
        ///   Asks instances of the specified service with the subtype to send details.
        /// </summary>
        /// <param name="service">
        ///   The service name to query. Typically of the form "_<i>service</i>._tcp".
        /// </param>
        /// <param name="subtype">
        ///   The feature that is needed.
        /// </param>
        /// <remarks>
        ///   When an answer is received the <see cref="ServiceInstanceDiscovered"/> event is raised.
        /// </remarks>
        /// <seealso cref="ServiceProfile.ServiceName"/>
        public void QueryServiceInstances(DomainName service, string subtype)
        {
            var name = DomainName.Join(
                new DomainName(subtype),
                SubName,
                service,
                LocalDomain);
            Mdns.SendQuery(name, type: DnsType.PTR);
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
        public void QueryUnicastServiceInstances(DomainName service)
        {
            Mdns.SendUnicastQuery(DomainName.Join(service, LocalDomain), type: DnsType.PTR);
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
        ///   <para>
        ///   Besides adding the profile's resource records to the <see cref="Catalog"/> PTR records are
        ///   created to support DNS-SD and reverse address mapping (DNS address lookup).
        ///   </para>
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

            foreach (var subtype in service.Subtypes)
            {
                var ptr = new PTRRecord
                {
                    Name = DomainName.Join(
                        new DomainName(subtype),
                        SubName,
                        service.QualifiedServiceName),
                    DomainName = service.FullyQualifiedName
                };
                catalog.Add(ptr, authoritative: true);
            }

            foreach (var r in service.Resources)
            {
                catalog.Add(r, authoritative: true);
            }

            catalog.IncludeReverseLookupRecords();
        }

        /// <summary>
        ///    Sends an unsolicited MDNS response describing the
        ///    service profile.
        /// </summary>
        /// <param name="profile">
        ///   The profile to describe.
        /// </param>
        /// <remarks>
        ///   Sends a MDNS response <see cref="Message"/> containing the pointer
        ///   and resource records of the <paramref name="profile"/>.
        ///   <para>
        ///   To provide increased robustness against packet loss,
        ///   two unsolicited responses are sent one second apart.
        ///   </para>
        /// </remarks>
        public void Announce(ServiceProfile profile)
        {
            var message = new Message { QR = true };

            // Add the shared records.
            var ptrRecord = new PTRRecord { Name = profile.QualifiedServiceName, DomainName = profile.FullyQualifiedName };
            message.Answers.Add(ptrRecord);

            // Add the resource records.
            profile.Resources.ForEach((resource) =>
            {
                message.Answers.Add(resource);
            });

            Mdns.SendAnswer(message, checkDuplicate: false);
            Task.Delay(1000).Wait();
            Mdns.SendAnswer(message, checkDuplicate: false);
        }

        /// <summary>
        /// Sends a goodbye message for the provided
        /// profile and removes its pointer from the name sever.
        /// </summary>
        /// <param name="profile">The profile to send a goodbye message for.</param>
        public void Unadvertise(ServiceProfile profile)
        {
            var message = new Message { QR = true };
            var ptrRecord = new PTRRecord { Name = profile.QualifiedServiceName, DomainName = profile.FullyQualifiedName };
            ptrRecord.TTL = TimeSpan.Zero;

            message.Answers.Add(ptrRecord);
            profile.Resources.ForEach((resource) =>
            {
                resource.TTL = TimeSpan.Zero;
                message.AdditionalRecords.Add(resource);
            });

            Mdns.SendAnswer(message);

            NameServer.Catalog.TryRemove(profile.QualifiedServiceName, out Node _);
        }

        /// <summary>
        /// Sends a goodbye message for each anounced service.
        /// </summary>
        public void Unadvertise()
        {
            profiles.ForEach(profile => Unadvertise(profile));
        }

        void OnAnswer(object sender, MessageEventArgs e)
        {
            var msg = e.Message;
            if (log.IsDebugEnabled)
            {
                log.Debug($"Answer from {e.RemoteEndPoint}");
            }
            if (log.IsTraceEnabled)
            {
                log.Trace(msg);
            }

            // Any DNS-SD answers?

            var sd = msg.Answers
                .OfType<PTRRecord>()
                .Where(ptr => ptr.Name.IsSubdomainOf(LocalDomain));
            foreach (var ptr in sd)
            {
                if (ptr.Name == ServiceName)
                {
                    ServiceDiscovered?.Invoke(this, ptr.DomainName);
                }
                else if (ptr.TTL == TimeSpan.Zero)
                {
                    var args = new ServiceInstanceShutdownEventArgs
                    {
                        ServiceInstanceName = ptr.DomainName,
                        Message = msg
                    };
                    ServiceInstanceShutdown?.Invoke(this, args);
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
			// Should run asynchronously because otherwise incoming messages may overlap and it may not be responded to every message.
			Task.Run(() =>
				{
					var request = e.Message;

					if (log.IsDebugEnabled)
					{
						log.Debug($"Query from {e.RemoteEndPoint}");
					}

					if (log.IsTraceEnabled)
					{
						log.Trace(request);
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
						response.AdditionalRecords.Clear();
					}

					if (!response.Answers.Any(a => a.Name == ServiceName))
					{
						;
					}

					// Only return address records that the querier can reach.
					response.RemoveUnreachableRecords(e.RemoteEndPoint.Address);

					if (QU)
					{
						// TODO: Send a Unicast response if required.
						Mdns.SendAnswer(response, e);
					}
					else
					{
						Mdns.SendAnswer(response, e);
					}

					if (log.IsDebugEnabled)
					{
						log.Debug($"Sending answer");
					}

					if (log.IsTraceEnabled)
					{
						log.Trace(response);
					}
				}).ConfigureAwait(false);
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
