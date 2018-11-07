using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
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
        static readonly ILog log = LogManager.GetLogger(typeof(MulticastService));
        static readonly IPAddress MulticastAddressIp4 = IPAddress.Parse("224.0.0.251");
        static readonly IPAddress MulticastAddressIp6 = IPAddress.Parse("FF02::FB");
        static readonly IPNetwork[] linkLocalNetworks = new[]
        {
            IPNetwork.Parse("169.254.0.0/16"),
            IPNetwork.Parse("fe80::/10")
        };

        const int MulticastPort = 5353;
        // IP header (20 bytes for IPv4; 40 bytes for IPv6) and the UDP header(8 bytes).
        const int packetOverhead = 48;
        const int maxDatagramSize = Message.MaxLength;

        CancellationTokenSource serviceCancellation;

        List<NetworkInterface> knownNics = new List<NetworkInterface>();
        readonly bool ip6;
        readonly IPEndPoint mdnsEndpoint;
        int maxPacketSize;

        /// <summary>
        /// Gets or sets a System.Boolean value that specifies whether outgoing multicast packets are delivered to the sending application.
        /// </summary>
        /// <value>
        /// true if the System.Net.Sockets.UdpClient receives outgoing multicast packets; otherwise, false.
        /// </value>
        public bool MulticastLoopback { get; set; } = false;

        /// <summary>
        ///   Recently sent messages.
        /// </summary>
        /// <value>
        ///   The key is the MD5 hash of the <see cref="Message"/> and the
        ///   value is when the message was sent.
        /// </value>
        /// <remarks>
        ///   This is used to avoid floding of responses as per
        ///   <see href="https://github.com/richardschneider/net-mdns/issues/18"/>
        /// </remarks>
        ConcurrentDictionary<long, DateTime> sentMessages = new ConcurrentDictionary<long, DateTime>();

        ConcurrentDictionary<IPAddress, UdpClient> senders = new ConcurrentDictionary<IPAddress, UdpClient>();

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
        /// <seealso cref="SendQuery(Message)"/>
        /// <see cref="SendAnswer"/>
        public event EventHandler<MessageEventArgs> QueryReceived;

        /// <summary>
        ///   Raised when any local MDNS service responds to a query.
        /// </summary>
        /// <value>
        ///   Contains the answer <see cref="Message"/>.
        /// </value>
        public event EventHandler<MessageEventArgs> AnswerReceived;

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
        public MulticastService()
        {
            if (Socket.OSSupportsIPv4)
                ip6 = false;
            else if (Socket.OSSupportsIPv6)
                ip6 = true;
            else
                throw new InvalidOperationException("No OS support for IPv4 nor IPv6");

            mdnsEndpoint = new IPEndPoint(
                ip6 ? MulticastAddressIp6 : MulticastAddressIp4,
                MulticastPort);
        }

        /// <summary>
        ///   Get the network interfaces that are useable.
        /// </summary>
        /// <returns>
        ///   A sequence of <see cref="NetworkInterface"/>.
        /// </returns>
        /// <remarks>
        ///   The following filters are applied
        ///   <list type="bullet">
        ///   <item><description>is enabled</description></item>
        ///   <item><description>not a loopback</description></item>
        ///   </list>
        /// </remarks>
        public static IEnumerable<NetworkInterface> GetNetworkInterfaces()
        {
            return NetworkInterface.GetAllNetworkInterfaces()
                .Where(nic => nic.OperationalStatus == OperationalStatus.Up)
                .Where(nic => nic.NetworkInterfaceType != NetworkInterfaceType.Loopback);
        }

        /// <summary>
        ///   Get the IP addresses of the local machine or specified NetworkInterface
        /// </summary>
        /// <param name="networkInterface">
        ///   The NetworkInterface.
        /// </param>
        /// <returns>
        ///   A sequence of IP addresses of the local machine.
        /// </returns>
        public static IEnumerable<IPAddress> GetIPAddresses(NetworkInterface networkInterface = null)
        {
            List<NetworkInterface> nics = new List<NetworkInterface>();

            if (networkInterface != null)
            {
                nics.Add(networkInterface);
            }
            else
            {
                nics.AddRange(GetNetworkInterfaces());
            }

            return nics
                .SelectMany(nic => nic.GetIPProperties().UnicastAddresses)
                .Select(u => u.Address)
                .Where(x => x.AddressFamily == AddressFamily.InterNetwork);
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
            serviceCancellation = new CancellationTokenSource();
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

            // Stop any long runnings tasks.
            using (var sc = serviceCancellation)
            {
                serviceCancellation = null;
                sc?.Cancel();
            }
        }

        void OnNetworkAddressChanged(object sender, EventArgs e) => FindNetworkInterfaces();

        void FindNetworkInterfaces()
        {
            var currentNics = GetNetworkInterfaces().ToList();

            var newNics = new List<NetworkInterface>();
            var oldNics = new List<NetworkInterface>();

            foreach (var nic in knownNics.Where(k => !currentNics.Any(n => k.Id == n.Id)))
            {
                oldNics.Add(nic);

                foreach (var address in GetIPAddresses(nic))
                {
                    if (senders.TryRemove(address, out var sender))
                    {
                        sender.Dispose();
                    }
                }

                if (log.IsDebugEnabled)
                {
                    log.Debug($"Removed nic '{nic.Name}'.");
                }
            }

            foreach (var nic in currentNics.Where(nic => !knownNics.Any(k => k.Id == nic.Id)))
            {
                newNics.Add(nic);

                foreach (var address in GetIPAddresses(nic))
                {
                    try
                    {
                        var client = CreateUdpClient(address);
                        if (senders.TryAdd(address, client))
                        {
                            Task.Factory.StartNew(async () =>
                                await ListenerAsync(address, client).ConfigureAwait(false));
                        }
                        else
                        {
                            client.Dispose();
                        }
                    }
                    catch (Exception ex)
                    {
                        log.Warn($"Error opening socket for: {address}", ex);
                        Console.Write($"Error opening socket for: {address}. {ex}");
                    }
                }

                if (log.IsDebugEnabled)
                {
                    log.Debug($"Found nic '{nic.Name}'.");
                }
            }

            knownNics = currentNics;

            // Tell others.
            if (newNics.Any())
            {
                NetworkInterfaceDiscovered?.Invoke(this, new NetworkInterfaceEventArgs
                {
                    NetworkInterfaces = newNics
                });
            }

            NetworkChange.NetworkAddressChanged -= OnNetworkAddressChanged;
            NetworkChange.NetworkAddressChanged += OnNetworkAddressChanged;
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
        public void SendQuery(string name, DnsClass klass = DnsClass.IN, DnsType type = DnsType.ANY)
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
            Send(msg, checkDuplicate: false);
        }

        /// <summary>
        ///   Send an answer to a query.
        /// </summary>
        /// <param name="answer">
        ///   The answer message.
        /// </param>
        /// <param name="localAddress">
        ///   Local IP Address of NIC that sould be used for unswering.
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
        ///   The <see cref="Message.AA"/> flag is set to true,
        ///   the <see cref="Message.Id"/> set to zero and any questions are removed.
        ///   <para>
        ///   The <paramref name="answer"/> is <see cref="Message.Truncate">truncated</see> 
        ///   if exceeds the maximum packet length.
        ///   </para>
        /// </remarks>
        /// <see cref="QueryReceived"/>
        /// <seealso cref="Message.CreateResponse"/>
        public void SendAnswer(Message answer, IPAddress localAddress = null, bool checkDuplicate = true)
        {
            // All MDNS answers are authoritative and have a transaction
            // ID of zero.
            answer.AA = true;
            answer.Id = 0;

            // All MDNS answers must not contain any questions.
            answer.Questions.Clear();

            answer.Truncate(maxPacketSize);

            Send(answer, localAddress, checkDuplicate);
        }

        void Send(Message msg, IPAddress localAddress = null, bool checkDuplicate = true)
        {
            var packet = msg.ToByteArray();
            if (packet.Length > maxPacketSize)
            {
                throw new ArgumentOutOfRangeException($"Exceeds max packet size of {maxPacketSize}.");
            }

            if (checkDuplicate)
            {
                // Get the hash of the packet.  MD5 is okay because
                // the hash is not used for security.
                var hash = GetHashCode(packet);

                // Prune the sent messages.  Anything older than a second ago
                // is removed.
                var dead = DateTime.Now.AddSeconds(-1);

                foreach (var notrecent in sentMessages.Where(x => x.Value < dead))
                {
                    sentMessages.TryRemove(notrecent.Key, out _);
                }

                // If messsage was recently sent, then do not send again.
                if (sentMessages.ContainsKey(hash))
                {
                    return;
                }

                sentMessages.AddOrUpdate(hash, DateTime.Now, (key, value) => value);
            }

            // Answer on Question in the same thread
            if (localAddress != null && senders.TryGetValue(localAddress, out var sender))
            {
                sender.Send(packet, packet.Length, mdnsEndpoint);
            }
            else
            {
                senders.Values.ToList().ForEach(x =>
                    Task.Factory.StartNew(async () =>
                        await x.SendAsync(packet, packet.Length, mdnsEndpoint).ConfigureAwait(false)));
            }
        }

        void OnDnsMessage(IPAddress localAddress, UdpReceiveResult message)
        {
            var msg = new Message();

            try
            {
                msg.Read(message.Buffer, 0, message.Buffer.Length);
            }
            catch (Exception e)
            {
                log.Warn("Received malformed message", e);
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
                    QueryReceived?.Invoke(this, new MessageEventArgs { Message = msg, LocalAddress = localAddress, RemoteEndPoint = message.RemoteEndPoint });
                }
                else if (msg.IsResponse && msg.Answers.Count > 0)
                {
                    AnswerReceived?.Invoke(this, new MessageEventArgs { Message = msg, LocalAddress = localAddress, RemoteEndPoint = message.RemoteEndPoint });
                }
            }
            catch (Exception e)
            {
                log.Error("Receive handler failed", e);
                // eat the exception
            }
        }

        UdpClient CreateUdpClient(IPAddress address)
        {
            var client = new UdpClient(AddressFamily.InterNetwork);
            client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            client.MulticastLoopback = MulticastLoopback;
            client.Client.Bind(new IPEndPoint(address, MulticastPort));
            client.JoinMulticastGroup(mdnsEndpoint.Address, address);

            return client;
        }

        async Task ListenerAsync(IPAddress localAddress, UdpClient receiver)
        {
            var cancel = serviceCancellation.Token;

            cancel.Register(() => receiver.Dispose());

            try
            {
                while (!cancel.IsCancellationRequested)
                {
                    var result = await receiver.ReceiveAsync().ConfigureAwait(false);

                    new Task(() => OnDnsMessage(localAddress, result)).Start();
                }
            }
            catch (Exception e)
            {
                if (!cancel.IsCancellationRequested)
                {
                    log.Error("Listener failed", e);
                }
                // eat the exception
            }

            senders.TryRemove(localAddress, out _);

            receiver.Dispose();
        }

        long GetHashCode(byte[] source)
        {
            using (var md5 = MD5.Create())
            {
                return BitConverter.ToInt64(md5.ComputeHash(source), 0);
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
