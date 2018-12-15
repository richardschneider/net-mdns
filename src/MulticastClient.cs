using Common.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Makaretu.Dns
{
    class MulticastClient : IDisposable
    {
        static readonly ILog log = LogManager.GetLogger(typeof(MulticastClient));

        readonly IPEndPoint multicastEndpoint;
        readonly IPAddress multicastLoopbackAddress;
        readonly UdpClient receiver;
        readonly ConcurrentDictionary<IPAddress, UdpClient> senders = new ConcurrentDictionary<IPAddress, UdpClient>();
        readonly bool IP6;

        public event EventHandler<UdpReceiveResult> MessageReceived;

        public MulticastClient(IPEndPoint multicastEndpoint, IEnumerable<NetworkInterface> nics)
        {
            IP6 = multicastEndpoint.AddressFamily == AddressFamily.InterNetworkV6;
            this.multicastEndpoint = multicastEndpoint;

            receiver = new UdpClient(multicastEndpoint.AddressFamily);
            receiver.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            receiver.Client.Bind(new IPEndPoint(IP6 ? IPAddress.IPv6Any : IPAddress.Any, multicastEndpoint.Port));
            if (IP6)
            {
                receiver.Client.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.AddMembership, new IPv6MulticastOption(multicastEndpoint.Address));
            }

            foreach (var address in nics.SelectMany(GetNetworkInterfaceLocalAddresses))
            {
                if (senders.Keys.Contains(address))
                {
                    continue;
                }

                var localEndpoint = new IPEndPoint(address, multicastEndpoint.Port);
                log.Debug($"Will send to {localEndpoint}");
                var sender = new UdpClient(multicastEndpoint.AddressFamily);
                try
                {
                    switch (address.AddressFamily)
                    {
                        case AddressFamily.InterNetwork:
                            receiver.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, new MulticastOption(multicastEndpoint.Address, address));
                            sender.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                            sender.Client.Bind(localEndpoint);
                            sender.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, new MulticastOption(multicastEndpoint.Address));
                            sender.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastLoopback, true);
                            break;
                        case AddressFamily.InterNetworkV6:
                            sender.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                            sender.Client.Bind(localEndpoint);
                            sender.Client.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.AddMembership, new IPv6MulticastOption(multicastEndpoint.Address));
                            sender.Client.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.MulticastLoopback, true);
                            break;
                        default:
                            throw new NotSupportedException($"Address family {address.AddressFamily}.");
                    }

                    // Assigning multicastLoopbackAddress to first avalable address that we use for sending messages
                    if (senders.TryAdd(address, sender) && multicastLoopbackAddress == null)
                    {
                        multicastLoopbackAddress = address;
                    }
                }
                catch (SocketException ex) when (ex.SocketErrorCode == SocketError.AddressNotAvailable)
                {
                    // VPN NetworkInterfaces
                    sender.Dispose();
                }
                catch (Exception)
                {
                    sender.Dispose();
                    throw;
                }
            }

            // Start listening for messages.
            Listen(receiver);
        }

        public async Task SendAsync(byte[] message)
        {
            await Task.WhenAll(senders.Select(x => x.Value.SendAsync(message, message.Length, multicastEndpoint))).ConfigureAwait(false);
        }

        void Listen(UdpClient receiver)
        {
            Task.Run(async () =>
            {
                try
                {
                    var task = receiver.ReceiveAsync();

                    _ = task.ContinueWith(x => Listen(receiver), TaskContinuationOptions.OnlyOnRanToCompletion | TaskContinuationOptions.RunContinuationsAsynchronously);

                    _ = task.ContinueWith(x => FilterMulticastLoopbackMessages(x.Result), TaskContinuationOptions.OnlyOnRanToCompletion | TaskContinuationOptions.RunContinuationsAsynchronously);

                    await task.ConfigureAwait(false);
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
            });
        }

        /// <summary>
        /// For multi NICs we accepting MulticastLoopback message only from one of available addresses, used for sending messages
        /// </summary>
        /// <param name="result">Received message <see cref="UdpReceiveResult"/></param>
        void FilterMulticastLoopbackMessages(UdpReceiveResult result)
        {
            log.Debug($"got datagram on {result.RemoteEndPoint}");

            var remoteIP = result.RemoteEndPoint.Address;

            if (senders.ContainsKey(remoteIP) && !remoteIP.Equals(multicastLoopbackAddress))
            {
                return;
            }

            MessageReceived?.Invoke(this, result);
        }

        IEnumerable<IPAddress> GetNetworkInterfaceLocalAddresses(NetworkInterface nic)
        {
            return nic.GetIPProperties().UnicastAddresses
                .Select(x => x.Address)
                .Where(x => x.AddressFamily == (IP6 ? AddressFamily.InterNetworkV6 : AddressFamily.InterNetwork));
        }

        #region IDisposable Support

        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    receiver?.Dispose();

                    foreach (var address in senders.Keys)
                    {
                        if (senders.TryRemove(address, out var sender))
                        {
                            sender.Dispose();
                        }
                    }
                }

                disposedValue = true;
            }
        }

        ~MulticastClient()
        {
            Dispose(false);
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
