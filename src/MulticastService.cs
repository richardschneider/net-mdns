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
        static readonly ILog log = LogManager.GetLogger(typeof(MulticastService));
        static readonly IPAddress MulticastAddressIp4 = IPAddress.Parse("224.0.0.251");
        static readonly IPAddress MulticastAddressIp6 = IPAddress.Parse("FF02::FB");

        const int MulticastPort = 5353;
        // IP header (20 bytes for IPv4; 40 bytes for IPv6) and the UDP header(8 bytes).
        const int packetOverhead = 48;
        const int maxDatagramSize = Message.MaxLength;

        CancellationTokenSource serviceCancellation;
        CancellationTokenSource listenerCancellation;

        List<NetworkInterface> knownNics = new List<NetworkInterface>();

        bool ip6;
        IPEndPoint mdnsEndpoint;
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
        ///   The multicast sender.
        /// </summary>
        /// <remarks>
        ///   Always use socketLock to gain access.
        /// </remarks>
        UdpClient sender;
        Object senderLock = new object();

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
        ///   <item><description>is enabled</description></item>
        ///   <item><description>not a loopback</description></item>
        ///   </list>
        /// </remarks>
        public IEnumerable<NetworkInterface> GetNetworkInterfaces()
        {
            return NetworkInterface.GetAllNetworkInterfaces()
                .Where(nic => nic.OperationalStatus == OperationalStatus.Up)
                .Where(nic => nic.NetworkInterfaceType != NetworkInterfaceType.Loopback);
        }

        /// <summary>
        ///   Get the IP addresses of the local machine.
        /// </summary>
        /// <returns>
        ///   A sequence of IP addresses of the local machine.
        /// </returns>
        public IEnumerable<IPAddress> GetIPAddresses()
        {
            return GetNetworkInterfaces()
                .SelectMany(nic => nic.GetIPProperties().UnicastAddresses)
                .Select(u => u.Address);
        }

        /// <summary>
        ///   Start the service.
        /// </summary>
        public void Start()
        {
            serviceCancellation = new CancellationTokenSource();
            maxPacketSize = maxDatagramSize - packetOverhead;
            knownNics.Clear();

            sender = new UdpClient(mdnsEndpoint.AddressFamily);
            sender.JoinMulticastGroup(mdnsEndpoint.Address);
            sender.MulticastLoopback = true;

            // Start a task to find the network interfaces.
            PollNetworkInterfaces();
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
            using (var lc = listenerCancellation)
            {
                listenerCancellation = null;
                lc?.Cancel();
            }

            if (sender != null)
            {
                sender.Dispose();
                sender = null;
            }
        }

        async void PollNetworkInterfaces()
        {
            var cancel = serviceCancellation.Token;
            try
            {
                while (!cancel.IsCancellationRequested)
                {
                    FindNetworkInterfaces();
                    await Task.Delay(NetworkInterfaceDiscoveryInterval, cancel);
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
            var nics = GetNetworkInterfaces().ToArray();
            var newNics = new List<NetworkInterface>();

            lock (senderLock)
            {
                foreach (var nic in nics.Where(nic => !knownNics.Any(k => k.Id == nic.Id)))
                {
                    newNics.Add(nic);
                    knownNics.Add(nic);
                }
            }

            // If any new NIC discovered.
            if (newNics.Any())
            {
                if (log.IsDebugEnabled)
                {
                    foreach (var nic in newNics)
                    {
                        log.Debug($"Found nic '{nic.Name}'.");
                    }
                }

                // Start a task to listen for MDNS messages.
                Listener();

                // Tell others.
                NetworkInterfaceDiscovered?.Invoke(this, new NetworkInterfaceEventArgs
                {
                    NetworkInterfaces = newNics
                });
            }
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
        public void SendQuery(string name, Class klass = Class.IN, DnsType type = DnsType.ANY)
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

        void Send(Message msg)
        {
            var packet = msg.ToByteArray();
            if (packet.Length > maxPacketSize)
            {
                throw new ArgumentOutOfRangeException($"Exceeds max packet size of {maxPacketSize}.");
            }

            lock (senderLock)
            {
                if (sender == null)
                    throw new InvalidOperationException("MDNS is not started");
                sender.SendAsync(packet, packet.Length, mdnsEndpoint).Wait();
                if (log.IsDebugEnabled)
                    log.Debug($"Sent msg to {mdnsEndpoint}");
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
            // Stop the previous listener.
            if (listenerCancellation != null)
            {
                listenerCancellation.Cancel();
            }

            listenerCancellation = new CancellationTokenSource();
            UdpClient receiver = new UdpClient(mdnsEndpoint.AddressFamily)
            {
                ExclusiveAddressUse = false
            };
            var endpoint = new IPEndPoint(ip6 ? IPAddress.IPv6Any : IPAddress.Any, MulticastPort);
            receiver.Client.SetSocketOption(
                SocketOptionLevel.Socket, 
                SocketOptionName.ReuseAddress,
                true);
            receiver.ExclusiveAddressUse = false;
            receiver.Client.Bind(endpoint);
            receiver.JoinMulticastGroup(mdnsEndpoint.Address);

            var cancel = listenerCancellation.Token;
            cancel.Register(() => receiver.Dispose());
            try
            {
                while (!cancel.IsCancellationRequested)
                {
                    var result = await receiver.ReceiveAsync();
                    if (log.IsDebugEnabled)
                        log.Debug($"Received msg from {result.RemoteEndPoint}");

                    OnDnsMessage(result.Buffer, result.Buffer.Length);
                }
            }
            catch (Exception e)
            {
                if (!cancel.IsCancellationRequested)
                    log.Error("Listener failed", e);
                // eat the exception
            }

            receiver.Dispose();
            listenerCancellation.Dispose();
            listenerCancellation = null;
        }
    }
}
