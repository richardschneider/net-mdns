using Common.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

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
    public class MulticastService
    {
        static ILog log = LogManager.GetLogger(typeof(MulticastService));

        IPAddress MulticastAddressIp4 = IPAddress.Parse("224.0.0.251");
        IPAddress MulticastAddressIp6 = IPAddress.Parse("FF02::FB");
        int MulticastPort = 5353;
        CancellationTokenSource listenerCancellation;
        List<NetworkInterface> knownNics = new List<NetworkInterface>();
        bool ip6;
        IPEndPoint mdnsEndpoint;
        // IP header (20 bytes for IPv4; 40 bytes for IPv6) and the UDP header(8 bytes).
        const int packetOverhead = 48;
        const int maxDatagramSize = Message.MaxLength;
        int maxPacketSize;

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
        ///   The multicast socket.
        /// </summary>
        /// <remarks>
        ///   Always use socketLock to gain access.
        /// </remarks>
        Socket socket;
        Object socketLock = new object();
        void CloseSocket()
        {
            lock (socketLock)
            {
                if (socket != null)
                {
                    socket.Dispose();
                    socket = null;
                }
            }
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

            mdnsEndpoint = new IPEndPoint(ip6 ? MulticastAddressIp6 : MulticastAddressIp4, MulticastPort);
        }

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
        TimeSpan NetworkInterfaceDiscoveryInterval { get; set; } = TimeSpan.FromMinutes(2);

        /// <summary>
        ///   Get the IP addresses of the local machine.
        /// </summary>
        /// <returns>
        ///   A sequence of IP addresses of the local machine.
        /// </returns>
        public static IEnumerable<IPAddress> GetIPAddresses()
        {
            return NetworkInterface.GetAllNetworkInterfaces()
                .Where(nic => nic.OperationalStatus == OperationalStatus.Up)
                .Where(nic => nic.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .Where(nic => nic.SupportsMulticast)
                .SelectMany(nic => nic.GetIPProperties().UnicastAddresses)
                .Select(u => u.Address);
        }

        /// <summary>
        ///   Start the service.
        /// </summary>
        public void Start()
        {
            maxPacketSize = maxDatagramSize - packetOverhead;
            listenerCancellation = new CancellationTokenSource();
            knownNics.Clear();
            socket = new Socket(
                ip6 ? AddressFamily.InterNetworkV6 : AddressFamily.InterNetwork,
                SocketType.Dgram,
                ProtocolType.Udp);
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            var endpoint = new IPEndPoint(ip6 ? IPAddress.IPv6Any : IPAddress.Any, MulticastPort);
            socket.Bind(endpoint);

            // Start a task to find the network interfaces.
            PollNetworkInterfaces();

            // Start a task to listen for MDNS messages.
            Listener();
        }

        async void PollNetworkInterfaces()
        {
            try
            {
                while (true)
                {
                    FindNetworkInterfaces();
                    await Task.Delay(NetworkInterfaceDiscoveryInterval, listenerCancellation.Token);
                }
            }
            catch (TaskCanceledException)
            {
                //  eat it
            }
            catch (Exception e)
            {
                log.Error(e);
                // eat it.
            }
        }

        void FindNetworkInterfaces()
        {
            var nics = NetworkInterface.GetAllNetworkInterfaces()
                .Where(nic => nic.OperationalStatus == OperationalStatus.Up)
                .Where(nic => nic.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .Where(nic => nic.SupportsMulticast)
                .Where(nic => !knownNics.Any(k => k.Id == nic.Id))
                .ToArray();
            foreach (var nic in nics)
            {
                lock (socketLock)
                {
                    if (socket == null)
                        return;

                    IPInterfaceProperties properties = nic.GetIPProperties();
                    if (ip6)
                    {
                        var interfaceIndex = properties.GetIPv6Properties().Index;
                        var mopt = new IPv6MulticastOption(MulticastAddressIp6, interfaceIndex);
                        socket.SetSocketOption(
                            SocketOptionLevel.IPv6,
                            SocketOptionName.AddMembership,
                            mopt);
                        maxPacketSize = Math.Min(maxPacketSize, properties.GetIPv6Properties().Mtu - packetOverhead);
                    }
                    else
                    {
                        var interfaceIndex = properties.GetIPv4Properties().Index;
                        var mopt = new MulticastOption(MulticastAddressIp4, interfaceIndex);
                        socket.SetSocketOption(
                            SocketOptionLevel.IP,
                            SocketOptionName.AddMembership,
                            mopt);
                        maxPacketSize = Math.Min(maxPacketSize, properties.GetIPv4Properties().Mtu - packetOverhead);
                    }
                    knownNics.Add(nic);
                }
            }

            // Tell others.
            if (nics.Length > 0)
            {
                lock (socketLock)
                {
                    if (socket == null)
                        return;
                    NetworkInterfaceDiscovered?.Invoke(this, new NetworkInterfaceEventArgs
                    {
                        NetworkInterfaces = nics
                    });
                }
            }
        }

        /// <summary>
        ///   Stop the service.
        /// </summary>
        /// <remarks>
        ///   Clears all the event handlers.
        /// </remarks>
        public void Stop()
        {
            QueryReceived = null;
            AnswerReceived = null;
            NetworkInterfaceDiscovered = null;
            if (listenerCancellation != null)
            {
                listenerCancellation.Cancel();
            }

            CloseSocket();
        }

        /// <summary>
        ///   Ask for answers about a name.
        /// </summary>
        /// <param name="name">
        ///   A domain name that should end with ".local", e.g. "myservice.local".
        /// </param>
        /// <param name="klass">
        ///   The class, defaults to <see cref="Class.IN"/>.
        /// </param>
        /// <remarks>
        ///   Answers to any query are obtained on the <see cref="AnswerReceived"/>
        ///   event.
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        ///   When the service has not started.
        /// </exception>
        public void SendQuery(string name, Class klass = Class.IN)
        {
            var msg = new Message
            {
                Opcode = MessageOperation.Query,
                QR = false
            };
            msg.Questions.Add(new Question
            {
                Name = name,
                Class = klass
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
            Send(msg);
        }

        /// <summary>
        ///   Send an answer to a query.
        /// </summary>
        /// <param name="answer">
        ///   The answer message.
        /// </param>
        /// <exception cref="InvalidOperationException">
        ///   When the service has not started.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        ///   When the serialised <paramref name="answer"/> is too large.
        /// </exception>
        /// <remarks>
        ///   The <see cref="Message.AA"/> flag is always set to true and
        ///   <see cref="Message.Id"/> set to zero.
        /// </remarks>
        /// <see cref="QueryReceived"/>
        /// <seealso cref="Message.CreateResponse"/>
        public void SendAnswer(Message answer)
        {
            // All MDNS answers are authoritative and have a transaction
            // ID of zero.
            answer.AA = true;
            answer.Id = 0;

            Send(answer);
        }

        private void Send(Message msg)
        {
            var packet = msg.ToByteArray();
            if (packet.Length > maxPacketSize)
            {
                throw new ArgumentOutOfRangeException($"Exceeds max packet size of {maxPacketSize}.");
            }

            lock (socketLock)
            {
                if (socket == null)
                    throw new InvalidOperationException("MDNS is not started");
                socket.SendTo(packet, 0, packet.Length, SocketFlags.None, mdnsEndpoint);
            }
        }

        /// <summary>
        ///   Called by the listener when a DNS message is received.
        /// </summary>
        /// <param name="datagram">
        ///   The received message.
        /// </param>
        /// <param name="length">
        ///   The length of the messages.
        /// </param>
        /// <remarks>
        ///   Decodes the <paramref name="datagram"/> and then raises
        ///   either the <see cref="QueryReceived"/> or <see cref="AnswerReceived"/> event.
        ///   <para>
        ///   Multicast DNS messages received with an OPCODE or RCODE other than zero 
        ///   are silently ignored.
        ///   </para>
        /// </remarks>
        void OnDnsMessage(byte[] datagram, int length)
        {
            var msg = new Message();

            try
            {
                msg.Read(datagram, 0, length);
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
                if (msg.IsQuery)
                {
                    QueryReceived?.Invoke(this, new MessageEventArgs { Message = msg });
                }
                else if (msg.IsResponse)
                {
                    AnswerReceived?.Invoke(this, new MessageEventArgs { Message = msg });
                }
            }
            catch (Exception e)
            {
                log.Error("Receive handler failed", e);
                // eat the exception
            }
        }

        /// <summary>
        ///   Listens for DNS messages.
        /// </summary>
        /// <remarks>
        ///   A background task to receive DNS messages from this and other MDNS services.  It is
        ///   cancelled via <see cref="Stop"/>.  All messages are forwarded to <see cref="OnDnsMessage"/>.
        /// </remarks>
        async void Listener()
        {
            var cancel = listenerCancellation.Token;

            cancel.Register(CloseSocket);
            var datagram = new byte[maxDatagramSize];
            var buffer = new ArraySegment<byte>(datagram);
            try
            {
                while (!cancel.IsCancellationRequested)
                {
                    var n = await socket.ReceiveAsync(buffer, SocketFlags.None);
                    if (n != 0 && !cancel.IsCancellationRequested)
                    {
                        OnDnsMessage(datagram, n);
                    }
                }
            }
            catch (Exception e)
            {
                if (!cancel.IsCancellationRequested)
                    log.Error("Listener failed", e);
                // eat the exception
            }
            CloseSocket();
        }
    }
}
