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
    public class Mdns
    {
        IPAddress MulticastAddressIp4 = IPAddress.Parse("224.0.0.251");
        IPAddress MulticastAddressIp6 = IPAddress.Parse("FF02::FB");
        int MulticastPort = 5353;
        CancellationTokenSource listenerCancellation;

        /// <summary>
        ///   The multicast socket.
        /// </summary>
        Socket socket;

        /// <summary>
        ///   Raised when any local service sends a query.
        /// </summary>
        /// <seealso cref="SendQuery(string)"/>
        /// <see cref="SendAnswer(object)"/>
        public event EventHandler<QueryEventArgs> Query;

        /// <summary>
        ///   Raised when any local service response to a query.
        /// </summary>
        public event EventHandler<AnswerEventArgs> Answer;

        /// <summary>
        ///   Start the service.
        /// </summary>
        public void Start()
        {
            listenerCancellation = new CancellationTokenSource();

            var ip6 = Socket.OSSupportsIPv6;
            socket = new Socket(
                ip6 ? AddressFamily.InterNetworkV6 : AddressFamily.InterNetwork,
                SocketType.Dgram,
                ProtocolType.Udp);
            //socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ExclusiveAddressUse, false);
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            //socket.ExclusiveAddressUse = false;
            var endpoint = new IPEndPoint(ip6 ? IPAddress.IPv6Any : IPAddress.Any, MulticastPort);
            socket.Bind(endpoint);

            var nics = NetworkInterface.GetAllNetworkInterfaces()
                .Where(nic => nic.OperationalStatus == OperationalStatus.Up)
                .Where(nic => nic.SupportsMulticast);
            foreach (var nic in nics)
            {
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
            }

            Task.Run((Action)Listener);
        }

        /// <summary>
        ///   Stop the service.
        /// </summary>
        public void Stop()
        {
            Query = null;
            Answer = null;
            listenerCancellation.Cancel();

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

            var ip6 = true;// Socket.OSSupportsIPv6;
            var endpoint = new IPEndPoint(ip6 ? MulticastAddressIp6 : MulticastAddressIp4, MulticastPort);
            socket.SendTo(new byte[10], endpoint);
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
        ///   either the <see cref="Query"/> or <see cref="Answer"/> event.
        /// </remarks>
        void OnDnsMessage(byte[] datagram, int length)
        {
            Console.WriteLine($"got datagram, {length} bytes");
        }

        /// <summary>
        ///   Listens for DNS messages.
        /// </summary>
        /// <remarks>
        ///   A background task to receive DNS messages from this and other MDNS services.  It is
        ///   cancelled via <see cref="Stop"/>.  All messages are forwarded to <see cref="OnDnsMessage"/>.
        /// </remarks>
        void Listener()
        {
            var cancel = listenerCancellation.Token;

            Console.WriteLine("start listening");
            cancel.Register(() =>
            {
                // .Net Standard on Unix neeeds this to cancel the Accept
#if false
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    socket.Shutdown(SocketShutdown.Both);
                }
#endif
                socket.Dispose();
                socket = null;
            });

            var datagram = new byte[32 * 1024];
            try
            {
                while (!cancel.IsCancellationRequested)
                {
                    var n = socket.Receive(datagram);
                    OnDnsMessage(datagram, n);
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
