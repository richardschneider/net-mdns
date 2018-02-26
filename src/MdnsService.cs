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
        ///   The multicast socket.
        /// </summary>
        Socket socket;

        /// <summary>
        ///   Raised when any local MDNS service sends a query.
        /// </summary>
        /// <seealso cref="SendQuery(string)"/>
        /// <see cref="SendAnswer(object)"/>
        public event EventHandler<QueryEventArgs> QueryReceived;

        /// <summary>
        ///   Raised when any local MDNS service responds to a query.
        /// </summary>
        public event EventHandler<AnswerEventArgs> AnswerReceived;

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
        ///   
        /// </summary>
        /// <param name="serviceName"></param>
        public void SendQuery(string serviceName)
        {
            if (socket == null)
                throw new InvalidOperationException("MDNS is not started");

            socket.SendTo(new byte[10], 0, 10, SocketFlags.None, mdnsEndpoint);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="answer"></param>
        public void SendAnswer(object answer)
        {
            throw new NotImplementedException();
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
            QueryReceived?.Invoke(this, new QueryEventArgs
            {
                Question = null // todo
            });
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
