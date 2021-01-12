using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Common.Logging;

namespace Makaretu.Dns
{
    /// <summary>
    ///   Muticast Domain Name Service.
    /// </summary>
    /// <remarks>
    ///   Sends and receives DNS queries and answers via the multicast mechachism
    ///   defined in <see href="https://tools.ietf.org/html/rfc6762"/>.
    ///   <para>
    ///   Use <see cref="Start"/> to start listening for multicast messages.
    ///   One of the events, <see cref="QueryReceived"/> or <see cref="AnswerReceived"/>, is
    ///   raised when a <see cref="Message"/> is received.
    ///   </para>
    /// </remarks>
    public class MulticastService : IResolver, IDisposable
    {
        // IP header (20 bytes for IPv4; 40 bytes for IPv6) and the UDP header(8 bytes).
        const int packetOverhead = 48;
        const int maxDatagramSize = Message.MaxLength;

        static readonly TimeSpan maxLegacyUnicastTTL = TimeSpan.FromSeconds(10);
        static readonly ILog log = LogManager.GetLogger(typeof(MulticastService));
        static readonly IPNetwork[] LinkLocalNetworks = new[] { IPNetwork.Parse("169.254.0.0/16"), IPNetwork.Parse("fe80::/10") };

        List<NetworkInterface> knownNics = new List<NetworkInterface>();
        int maxPacketSize;

        /// <summary>
        ///   Recently sent messages.
        /// </summary>
        RecentMessages sentMessages = new RecentMessages();

        /// <summary>
        ///   Recently received messages.
        /// </summary>
        RecentMessages receivedMessages = new RecentMessages();

        /// <summary>
        ///   The multicast client.
        /// </summary>
        MulticastClient client;

        /// <summary>
        ///   Use to send unicast IPv4 answers.
        /// </summary>
        UdpClient unicastClientIp4 = new UdpClient(AddressFamily.InterNetwork);

        /// <summary>
        ///   Use to send unicast IPv6 answers.
        /// </summary>
        UdpClient unicastClientIp6 = new UdpClient(AddressFamily.InterNetworkV6);

        /// <summary>
        ///   Function used for listening filtered network interfaces.
        /// </summary>
        Func<IEnumerable<NetworkInterface>, IEnumerable<NetworkInterface>> networkInterfacesFilter;

        /// <summary>
        ///   Set the default TTLs.
        /// </summary>
        /// <seealso cref="ResourceRecord.DefaultTTL"/>
        /// <seealso cref="ResourceRecord.DefaultHostTTL"/>
        static MulticastService()
        {
            // https://tools.ietf.org/html/rfc6762 section 10
            ResourceRecord.DefaultTTL = TimeSpan.FromMinutes(75);
            ResourceRecord.DefaultHostTTL = TimeSpan.FromSeconds(120);
        }

        /// <summary>
        ///   Raised when any local MDNS service sends a query.
        /// </summary>
        /// <value>
        ///   Contains the query <see cref="Message"/>.
        /// </value>
        /// <remarks>
        ///   Any exception throw by the event handler is simply logged and
        ///   then forgotten.
        /// </remarks>
        /// <seealso cref="SendQuery(Message)"/>
        public event EventHandler<MessageEventArgs> QueryReceived;

        /// <summary>
        ///   Raised when any link-local MDNS service responds to a query.
        /// </summary>
        /// <value>
        ///   Contains the answer <see cref="Message"/>.
        /// </value>
        /// <remarks>
        ///   Any exception throw by the event handler is simply logged and
        ///   then forgotten.
        /// </remarks>
        public event EventHandler<MessageEventArgs> AnswerReceived;

        /// <summary>
        ///   Raised when a DNS message is received that cannot be decoded.
        /// </summary>
        /// <value>
        ///   The DNS message as a byte array.
        /// </value>
        public event EventHandler<byte[]> MalformedMessage;

        /// <summary>
        ///   Raised when one or more network interfaces are discovered. 
        /// </summary>
        /// <value>
        ///   Contains the network interface(s).
        /// </value>
        public event EventHandler<NetworkInterfaceEventArgs> NetworkInterfaceDiscovered;

        /// <summary>
        ///   Create a new instance of the <see cref="MulticastService"/> class.
        /// </summary>
        /// <param name="filter">
        ///   Multicast listener will be bound to result of filtering function.
        /// </param>
        public MulticastService(Func<IEnumerable<NetworkInterface>, IEnumerable<NetworkInterface>> filter = null)
        {
            networkInterfacesFilter = filter;

            UseIpv4 = Socket.OSSupportsIPv4;
            UseIpv6 = Socket.OSSupportsIPv6;
            IgnoreDuplicateMessages = true;
        }

        /// <summary>
        ///   Send and receive on IPv4.
        /// </summary>
        /// <value>
        ///   Defaults to <b>true</b> if the OS supports it.
        /// </value>
        public bool UseIpv4 { get; set; }

        /// <summary>
        ///   Send and receive on IPv6.
        /// </summary>
        /// <value>
        ///   Defaults to <b>true</b> if the OS supports it.
        /// </value>
        public bool UseIpv6 { get; set; }

        /// <summary>
        ///   Determines if received messages are checked for duplicates.
        /// </summary>
        /// <value>
        ///   <b>true</b> to ignore duplicate messages. Defaults to <b>true</b>.
        /// </value>
        /// <remarks>
        ///   When set, a message that has been received within the last minute
        ///   will be ignored.
        /// </remarks>
        public bool IgnoreDuplicateMessages { get; set; }

        /// <summary>
        /// Determines whether loopback interfaces should be excluded when other network interfaces are available
        /// </summary>
        /// <value>
        /// <b>true</b> to include loopback interfaces also when other network interfaces are up. Defaults to <b>false</b>.
        /// </value>
        /// <remarks>
        /// If not set, loopback network interfaces will be ignored regardless of the network interface filter.
        /// </remarks>
        public static bool IncludeLoopbackInterfaces { get; set; } = false;

        /// <summary>
        ///   The interval for discovering network interfaces.
        /// </summary>
        /// <value>
        ///   Default is 2 minutes.
        /// </value>
        /// <remarks>
        ///   When the interval is reached a task is started to discover any
        ///   new network interfaces. 
        /// </remarks>
        /// <seealso cref="NetworkInterfaceDiscovered"/>
        [Obsolete("This property is deprecated and will be removed in nearest future. Using timer removed with obsording of NetworkChange.NetworkAddressChanged event.", false)]
        public TimeSpan NetworkInterfaceDiscoveryInterval { get; set; } = TimeSpan.FromMinutes(2);

        /// <summary>
        ///   Get the network interfaces that are useable.
        /// </summary>
        /// <returns>
        ///   A sequence of <see cref="NetworkInterface"/>.
        /// </returns>
        /// <remarks>
        ///   The following filters are applied
        ///   <list type="bullet">
        ///   <item><description>interface is enabled</description></item>
        ///   <item><description>interface is not a loopback</description></item>
        ///   </list>
        ///   <para>
        ///   If no network interface is operational, then the loopback interface(s)
        ///   are included (127.0.0.1 and/or ::1).
        ///   </para>
        /// </remarks>
        public static IEnumerable<NetworkInterface> GetNetworkInterfaces()
        {
            if(!IncludeLoopbackInterfaces)
            {
                var nics = NetworkInterface.GetAllNetworkInterfaces()
                    .Where( nic => nic.OperationalStatus == OperationalStatus.Up )
                    .Where( nic => nic.NetworkInterfaceType != NetworkInterfaceType.Loopback )
                    .ToArray();
                if(nics.Length > 0)
                    return nics;
            }
            // Special case: no operational NIC, then use loopbacks.
            return NetworkInterface.GetAllNetworkInterfaces()
                .Where(nic => nic.OperationalStatus == OperationalStatus.Up);
        }

        /// <summary>
        ///   Get the IP addresses of the local machine.
        /// </summary>
        /// <returns>
        ///   A sequence of IP addresses of the local machine.
        /// </returns>
        /// <remarks>
        ///   The loopback addresses (127.0.0.1 and ::1) are NOT included in the
        ///   returned sequences.
        /// </remarks>
        public static IEnumerable<IPAddress> GetIPAddresses()
        {
            return GetNetworkInterfaces()
                .SelectMany(nic => nic.GetIPProperties().UnicastAddresses)
                .Select(u => u.Address);
        }

        /// <summary>
        ///   Get the link local IP addresses of the local machine.
        /// </summary>
        /// <returns>
        ///   A sequence of IP addresses.
        /// </returns>
        /// <remarks>
        ///   All IPv4 addresses are considered link local.
        /// </remarks>
        /// <seealso href="https://en.wikipedia.org/wiki/Link-local_address"/>
        public static IEnumerable<IPAddress> GetLinkLocalAddresses()
        {
            return GetIPAddresses()
                .Where(a => a.AddressFamily == AddressFamily.InterNetwork ||
                    (a.AddressFamily == AddressFamily.InterNetworkV6 && a.IsIPv6LinkLocal));
        }

        /// <summary>
        ///   Start the service.
        /// </summary>
        public void Start()
        {
            maxPacketSize = maxDatagramSize - packetOverhead;

            knownNics.Clear();

            FindNetworkInterfaces();
        }

        /// <summary>
        ///   Stop the service.
        /// </summary>
        /// <remarks>
        ///   Clears all the event handlers.
        /// </remarks>
        public void Stop()
        {
            // All event handlers are cleared.
            QueryReceived = null;
            AnswerReceived = null;
            NetworkInterfaceDiscovered = null;

            // Stop current UDP listener
            client?.Dispose();
            client = null;
        }

        void OnNetworkAddressChanged(object sender, EventArgs e) => FindNetworkInterfaces();

        void FindNetworkInterfaces()
        {
            log.Debug("Finding network interfaces");

            try
            {
                var currentNics = GetNetworkInterfaces().ToList();

                var newNics = new List<NetworkInterface>();
                var oldNics = new List<NetworkInterface>();

                foreach (var nic in knownNics.Where(k => !currentNics.Any(n => k.Id == n.Id)))
                {
                    oldNics.Add(nic);

                    if (log.IsDebugEnabled)
                    {
                        log.Debug($"Removed nic '{nic.Name}'.");
                    }
                }

                foreach (var nic in currentNics.Where(nic => !knownNics.Any(k => k.Id == nic.Id)))
                {
                    newNics.Add(nic);

                    if (log.IsDebugEnabled)
                    {
                        log.Debug($"Found nic '{nic.Name}'.");
                    }
                }

                knownNics = currentNics;

                // Only create client if something has change.
                if (newNics.Any() || oldNics.Any())
                {
                    client?.Dispose();
                    client = new MulticastClient(UseIpv4, UseIpv6, networkInterfacesFilter?.Invoke(knownNics) ?? knownNics);
                    client.MessageReceived += OnDnsMessage;
                }

                // Tell others.
                if (newNics.Any())
                {
                    NetworkInterfaceDiscovered?.Invoke(this, new NetworkInterfaceEventArgs
                    {
                        NetworkInterfaces = newNics
                    });
                }

                // Magic from @eshvatskyi
                //
                // I've seen situation when NetworkAddressChanged is not triggered 
                // (wifi off, but NIC is not disabled, wifi - on, NIC was not changed 
                // so no event). Rebinding fixes this.
                //
                // Do magic only on Windows.
#if NET461
                if (Environment.OSVersion.Platform.ToString().StartsWith("Win"))
#else
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
#endif
                {
                    NetworkChange.NetworkAddressChanged -= OnNetworkAddressChanged;
                    NetworkChange.NetworkAddressChanged += OnNetworkAddressChanged;
                }
            }
            catch (Exception e)
            {
                log.Error("FindNics failed", e);
            }
        }

        /// <inheritdoc />
        public Task<Message> ResolveAsync(Message request, CancellationToken cancel = default(CancellationToken))
        {
            var tsc = new TaskCompletionSource<Message>();

            void checkResponse(object s, MessageEventArgs e)
            {
                var response = e.Message;
                if (request.Questions.All(q => response.Answers.Any(a => a.Name == q.Name)))
                {
                    AnswerReceived -= checkResponse;
                    tsc.SetResult(response);
                }
            }

            cancel.Register(() =>
            {
                AnswerReceived -= checkResponse;
                tsc.TrySetCanceled();
            });

            AnswerReceived += checkResponse;
            SendQuery(request);

            return tsc.Task;
        }

        /// <summary>
        ///   Ask for answers about a name.
        /// </summary>
        /// <param name="name">
        ///   A domain name that should end with ".local", e.g. "myservice.local".
        /// </param>
        /// <param name="klass">
        ///   The class, defaults to <see cref="DnsClass.IN"/>.
        /// </param>
        /// <param name="type">
        ///   The question type, defaults to <see cref="DnsType.ANY"/>.
        /// </param>
        /// <remarks>
        ///   Answers to any query are obtained on the <see cref="AnswerReceived"/>
        ///   event.
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        ///   When the service has not started.
        /// </exception>
        public void SendQuery(DomainName name, DnsClass klass = DnsClass.IN, DnsType type = DnsType.ANY)
        {
            var msg = new Message
            {
                Opcode = MessageOperation.Query,
                QR = false
            };
            msg.Questions.Add(new Question
            {
                Name = name,
                Class = klass,
                Type = type
            });

            SendQuery(msg);
        }

        /// <summary>
        ///   Ask for answers about a name and accept unicast and/or broadcast response.
        /// </summary>
        /// <param name="name">
        ///   A domain name that should end with ".local", e.g. "myservice.local".
        /// </param>
        /// <param name="klass">
        ///   The class, defaults to <see cref="DnsClass.IN"/>.
        /// </param>
        /// <param name="type">
        ///   The question type, defaults to <see cref="DnsType.ANY"/>.
        /// </param>
        /// <remarks>
        ///   Send a "QU" question (unicast).  The most significat bit of the Class is set.
        ///   Answers to any query are obtained on the <see cref="AnswerReceived"/>
        ///   event.
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        ///   When the service has not started.
        /// </exception>
        public void SendUnicastQuery(DomainName name, DnsClass klass = DnsClass.IN, DnsType type = DnsType.ANY)
        {
            var msg = new Message
            {
                Opcode = MessageOperation.Query,
                QR = false
            };
            msg.Questions.Add(new Question
            {
                Name = name,
                Class = (DnsClass) ((ushort)klass | 0x8000),
                Type = type
            });

            SendQuery(msg);
        }

        /// <summary>
        ///   Ask for answers.
        /// </summary>
        /// <param name="msg">
        ///   A query message.
        /// </param>
        /// <remarks>
        ///   Answers to any query are obtained on the <see cref="AnswerReceived"/>
        ///   event.
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        ///   When the service has not started.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        ///   When the serialised <paramref name="msg"/> is too large.
        /// </exception>
        public void SendQuery(Message msg)
        {
            Send(msg, false);
        }

        /// <summary>
        ///   Send an answer to a query.
        /// </summary>
        /// <param name="answer">
        ///   The answer message.
        /// </param>
        /// <param name="checkDuplicate">
        ///   If <b>true</b>, then if the same <paramref name="answer"/> was
        ///   recently sent it will not be sent again.
        /// </param>
        /// <exception cref="InvalidOperationException">
        ///   When the service has not started.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        ///   When the serialised <paramref name="answer"/> is too large.
        /// </exception>
        /// <remarks>
        ///   <para>
        ///   The <see cref="Message.AA"/> flag is set to true,
        ///   the <see cref="Message.Id"/> set to zero and any questions are removed.
        ///   </para>
        ///   <para>
        ///   The <paramref name="answer"/> is <see cref="Message.Truncate">truncated</see> 
        ///   if exceeds the maximum packet length.
        ///   </para>
        ///   <para>
        ///   <paramref name="checkDuplicate"/> should always be <b>true</b> except
        ///   when <see href="https://tools.ietf.org/html/rfc6762#section-8.1">answering a probe</see>.
        ///   </para>
        ///   <note type="caution">
        ///   If possible the <see cref="SendAnswer(Message, MessageEventArgs, bool)"/>
        ///   method should be used, so that legacy unicast queries are supported.
        ///   </note>
        /// </remarks>
        /// <see cref="QueryReceived"/>
        /// <seealso cref="Message.CreateResponse"/>
        public void SendAnswer(Message answer, bool checkDuplicate = true)
        {
            // All MDNS answers are authoritative and have a transaction
            // ID of zero.
            answer.AA = true;
            answer.Id = 0;

            // All MDNS answers must not contain any questions.
            answer.Questions.Clear();

            answer.Truncate(maxPacketSize);

            Send(answer, checkDuplicate);
        }

        /// <summary>
        ///   Send an answer to a query.
        /// </summary>
        /// <param name="answer">
        ///   The answer message.
        /// </param>
        /// <param name="query">
        ///   The query that is being answered.
        /// </param>
        /// <param name="checkDuplicate">
        ///   If <b>true</b>, then if the same <paramref name="answer"/> was
        ///   recently sent it will not be sent again.
        /// </param>
        /// <exception cref="InvalidOperationException">
        ///   When the service has not started.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        ///   When the serialised <paramref name="answer"/> is too large.
        /// </exception>
        /// <remarks>
        ///   <para>
        ///   If the <paramref name="query"/> is a standard multicast query (sent to port 5353), then 
        ///   <see cref="SendAnswer(Message, bool)"/> is called.
        ///   </para>
        ///   <para>
        ///   Otherwise a legacy unicast reponse is sent to sender's end point.
        ///   The <see cref="Message.AA"/> flag is set to true,
        ///   the <see cref="Message.Id"/> is set to query's ID,
        ///   the <see cref="Message.Questions"/> is set to the query's questions,
        ///   and all resource record TTLs have a max value of 10 seconds.
        ///   </para>
        ///   <para>
        ///   The <paramref name="answer"/> is <see cref="Message.Truncate">truncated</see> 
        ///   if exceeds the maximum packet length.
        ///   </para>
        ///   <para>
        ///   <paramref name="checkDuplicate"/> should always be <b>true</b> except
        ///   when <see href="https://tools.ietf.org/html/rfc6762#section-8.1">answering a probe</see>.
        ///   </para>
        /// </remarks>
        public void SendAnswer(Message answer, MessageEventArgs query, bool checkDuplicate = true)
        {
            if (!query.IsLegacyUnicast)
            {
                SendAnswer(answer, checkDuplicate);
                return;
            }

            answer.AA = true;
            answer.Id = query.Message.Id;
            answer.Questions.Clear();
            answer.Questions.AddRange(query.Message.Questions);
            answer.Truncate(maxPacketSize);

            foreach (var r in answer.Answers)
            {
                r.TTL = (r.TTL > maxLegacyUnicastTTL) ? maxLegacyUnicastTTL : r.TTL;
            }
            foreach (var r in answer.AdditionalRecords)
            {
                r.TTL = (r.TTL > maxLegacyUnicastTTL) ? maxLegacyUnicastTTL : r.TTL;
            }
            foreach (var r in answer.AdditionalRecords)
            {
                r.TTL = (r.TTL > maxLegacyUnicastTTL) ? maxLegacyUnicastTTL : r.TTL;
            }

            Send(answer, checkDuplicate, query.RemoteEndPoint);
        }

        void Send(Message msg, bool checkDuplicate, IPEndPoint remoteEndPoint = null)
        {
            var packet = msg.ToByteArray();
            if (packet.Length > maxPacketSize)
            {
                throw new ArgumentOutOfRangeException($"Exceeds max packet size of {maxPacketSize}.");
            }

            if (checkDuplicate && !sentMessages.TryAdd(packet))
            {
                return;
            }

            // Standard multicast reponse?
            if (remoteEndPoint == null)
            {
                client?.SendAsync(packet).GetAwaiter().GetResult();
            }
            // Unicast response
            else
            {
                var unicastClient = (remoteEndPoint.Address.AddressFamily == AddressFamily.InterNetwork)
                    ? unicastClientIp4 : unicastClientIp6;
                unicastClient.SendAsync(packet, packet.Length, remoteEndPoint).GetAwaiter().GetResult();
            }
        }

        /// <summary>
        ///   Called by the MulticastClient when a DNS message is received.	
        /// </summary>	
        /// <param name="sender">
        ///   The <see cref="MulticastClient"/> that got the message.
        /// </param>
        /// <param name="result">	
        ///   The received message <see cref="UdpReceiveResult"/>.	
        /// </param>	
        /// <remarks>	
        ///   Decodes the <paramref name="result"/> and then raises	
        ///   either the <see cref="QueryReceived"/> or <see cref="AnswerReceived"/> event.	
        ///   <para>	
        ///   Multicast DNS messages received with an OPCODE or RCODE other than zero 	
        ///   are silently ignored.	
        ///   </para>	
        ///   <para>
        ///   If the message cannot be decoded, then the <see cref="MalformedMessage"/>
        ///   event is raised.
        ///   </para>
        /// </remarks>
        public void OnDnsMessage(object sender, UdpReceiveResult result)
        {
            // If recently received, then ignore.
            if (IgnoreDuplicateMessages && !receivedMessages.TryAdd(result.Buffer))
            {
                return;
            }

            var msg = new Message();
            try
            {
                msg.Read(result.Buffer, 0, result.Buffer.Length);
            }
            catch (Exception e)
            {
                log.Warn("Received malformed message", e);
                MalformedMessage?.Invoke(this, result.Buffer);
                return; // eat the exception
            }

            if (msg.Opcode != MessageOperation.Query || msg.Status != MessageStatus.NoError)
            {
                return;
            }

            // Dispatch the message.
            try
            {
                if (msg.IsQuery && msg.Questions.Count > 0)
                {
                    QueryReceived?.Invoke(this, new MessageEventArgs { Message = msg, RemoteEndPoint = result.RemoteEndPoint });
                }
                else if (msg.IsResponse && msg.Answers.Count > 0)
                {
                    AnswerReceived?.Invoke(this, new MessageEventArgs { Message = msg, RemoteEndPoint = result.RemoteEndPoint });
                }
            }
            catch (Exception e)
            {
                log.Error("Receive handler failed", e);
                // eat the exception
            }
        }

#region IDisposable Support

        /// <inheritdoc />
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                Stop();
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
