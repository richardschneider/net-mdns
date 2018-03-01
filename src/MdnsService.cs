using Makaretu.Dns;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Makaretu.Mdns
{
    /// <summary>
    ///   Muticast Domain Name Service
    /// </summary>
    public class MdnsService
    {
        IPAddress MulticastAddressIp4 = IPAddress.Parse("224.0.0.251");
        IPAddress MulticastAddressIp6 = IPAddress.Parse("FF02::FB");
        int MulticastPort = 5353;
        CancellationTokenSource listenerCancellation;
        List<NetworkInterface> knownNics = new List<NetworkInterface>();
        Timer nicTimer;
        bool ip6;
        IPEndPoint mdnsEndpoint;

        /// <summary>
        ///   Set the default TTLs.
        /// </summary>
        /// <seealso cref="ResourceRecord.DefaultTTL"/>
        /// <seealso cref="ResourceRecord.DefaultHostTTL"/>
        static MdnsService()
        {
            // https://tools.ietf.org/html/rfc6762 section 10
            ResourceRecord.DefaultTTL = TimeSpan.FromMinutes(75);
            ResourceRecord.DefaultHostTTL = TimeSpan.FromSeconds(120);
        }

        /// <summary>
        ///   The multicast socket.
        /// </summary>
        Socket socket;

        /// <summary>
        ///   Raised when any local MDNS service sends a query.
        /// </summary>
        /// <seealso cref="SendQuery(Message)"/>
        /// <see cref="SendAnswer"/>
        public event EventHandler<MessageEventArgs> QueryReceived;

        /// <summary>
        ///   Raised when any local MDNS service responds to a query.
        /// </summary>
        public event EventHandler<MessageEventArgs> AnswerReceived;

        /// <summary>
        ///   Raised when one or more network interfaces are discovered. 
        /// </summary>
        public event EventHandler<NetworkInterfaceEventArgs> NetworkInterfaceDiscovered;

        /// <summary>
        ///   Create a new instance of the <see cref="MdnsService"/> class.
        /// </summary>
        public MdnsService()
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
        ///   Start the service.
        /// </summary>
        public void Start()
        {
            listenerCancellation = new CancellationTokenSource();
            knownNics.Clear();
            socket = new Socket(
                ip6 ? AddressFamily.InterNetworkV6 : AddressFamily.InterNetwork,
                SocketType.Dgram,
                ProtocolType.Udp);
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            var endpoint = new IPEndPoint(ip6 ? IPAddress.IPv6Any : IPAddress.Any, MulticastPort);
            socket.Bind(endpoint);

            // Start a task to find the network interface.
            nicTimer = new Timer(
                FindNetworkInterfaces, 
                this, 
                TimeSpan.Zero, 
                NetworkInterfaceDiscoveryInterval);

            // Start a task to listen for MDNS messages.
            Task.Run((Action)Listener, listenerCancellation.Token);
        }

        void FindNetworkInterfaces(object state)
        {
            var nics = NetworkInterface.GetAllNetworkInterfaces()
                .Where(nic => nic.OperationalStatus == OperationalStatus.Up)
                .Where(nic => nic.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .Where(nic => nic.SupportsMulticast)
                .Where(nic => !knownNics.Any(k => k.Id == nic.Id))
                .ToArray();
            foreach (var nic in nics)
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
                }
                else
                {
                    var interfaceIndex = properties.GetIPv4Properties().Index;
                    var mopt = new MulticastOption(MulticastAddressIp4, interfaceIndex);
                    socket.SetSocketOption(
                        SocketOptionLevel.IP,
                        SocketOptionName.AddMembership,
                        mopt);
                }
                knownNics.Add(nic);
            }

            // Tell others.
            if (nics.Length > 0)
            {
                NetworkInterfaceDiscovered?.Invoke(this, new NetworkInterfaceEventArgs
                {
                    NetworkInterfaces = nics
                });
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
            if (nicTimer != null)
            {
                nicTimer.Dispose();
                nicTimer = null;
            }
            if (listenerCancellation != null)
            {
                listenerCancellation.Cancel();
            }

            if (socket != null)
            {
                socket.Dispose();
                socket = null;
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
        /// <remarks>
        ///   Answers to any query are obtained on the <see cref="AnswerReceived"/>
        ///   event.
        /// </remarks>
        public void SendQuery(string name, Class klass = Class.IN)
        {
            if (socket == null)
                throw new InvalidOperationException("MDNS is not started");

            var msg = new Message
            {
                Id = 1,
                OPCODE = Message.Opcode.QUERY,
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
        public void SendQuery(Message msg)
        {
            var packet = msg.ToByteArray();
            socket.SendTo(packet, 0, packet.Length, SocketFlags.None, mdnsEndpoint);
        }

        /// <summary>
        ///   Send an answer to a query.
        /// </summary>
        /// <param name="answer">
        ///   The answer message.
        /// </param>
        /// <see cref="QueryReceived"/>
        /// <seealso cref="Message.CreateResponse"/>
        public void SendAnswer(Message answer)
        {
            // All MDNS answers are authoritative.
            answer.AA = true;

            var packet = answer.ToByteArray();
            socket.SendTo(packet, 0, packet.Length, SocketFlags.None, mdnsEndpoint);
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
        /// </remarks>
        void OnDnsMessage(byte[] datagram, int length)
        {
            Console.WriteLine($"got datagram, {length} bytes");
            var msg = new Message();
            // TODO: log and ignore message format errors.
            msg.Read(datagram, 0, length);

            // Dispatch the message.
            // TODO: error handling
            if (msg.IsQuery)
            {
                QueryReceived?.Invoke(this, new MessageEventArgs { Message = msg });
            }
            if (msg.IsResponse)
            {
                AnswerReceived?.Invoke(this, new MessageEventArgs { Message = msg });
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

            Console.WriteLine("start listening");
            cancel.Register(() =>
            {
                if (socket != null)
                {
                    socket.Dispose();
                    socket = null;
                }
            });
            var datagram = new byte[8 * 1024];
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
                    Console.WriteLine(e.Message);
            }
            if (socket != null)
            {
                socket.Dispose();
                socket = null;
            }
            Console.WriteLine("stop listening");
        }
    }
}
