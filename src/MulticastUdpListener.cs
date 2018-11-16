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
    class MulticastUdpListener : IDisposable
    {
        public static readonly bool IP6;

        private readonly IPEndPoint multicastEndpoint;

        UdpClient receiver;
        ConcurrentDictionary<IPAddress, UdpClient> senders = new ConcurrentDictionary<IPAddress, UdpClient>();
        List<IPAddress> addresses = new List<IPAddress>();

        public IReadOnlyCollection<IPAddress> Addresses => addresses;

        static MulticastUdpListener()
        {
            if (Socket.OSSupportsIPv4)
                IP6 = false;
            else if (Socket.OSSupportsIPv6)
                IP6 = true;
            else
                throw new InvalidOperationException("No OS support for IPv4 nor IPv6");
        }

        public MulticastUdpListener(IPEndPoint multicastEndpoint, IEnumerable<NetworkInterface> nics)
        {
            this.multicastEndpoint = multicastEndpoint;

            receiver = new UdpClient(multicastEndpoint.AddressFamily);

            receiver.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            receiver.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ExclusiveAddressUse, false);

            receiver.Client.Bind(new IPEndPoint(IP6 ? IPAddress.IPv6Any : IPAddress.Any, multicastEndpoint.Port));

            foreach (var address in nics.SelectMany(GetNetworkInterfaceLocalAddresses))
            {
                try
                {
                    receiver.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, new MulticastOption(multicastEndpoint.Address, address));

                    var sender = new UdpClient(multicastEndpoint.AddressFamily);

                    sender.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                    sender.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ExclusiveAddressUse, false);

                    sender.Client.Bind(new IPEndPoint(address, multicastEndpoint.Port));

                    sender.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, new MulticastOption(multicastEndpoint.Address));
                    sender.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastLoopback, true);

                    senders.TryAdd(address, sender);

                    addresses.Add(address);
                }
                catch (SocketException ex) when (ex.SocketErrorCode == SocketError.AddressNotAvailable)
                {
                    // VPN NetworkInterfaces
                }
            }
        }

        public void SendAsync(byte[] message)
        {
            senders.ToList().ForEach(x => x.Value.SendAsync(message, message.Length, multicastEndpoint));
        }

        public void ListenAsync(Action<UdpReceiveResult> callback)
        {
            Task.Run(async () =>
            {
                try
                {
                    var task = receiver.ReceiveAsync();

                    var ct1 = task.ContinueWith(x => ListenAsync(callback), TaskContinuationOptions.OnlyOnRanToCompletion | TaskContinuationOptions.RunContinuationsAsynchronously);

                    var ct2 = task.ContinueWith(x => FilterMulticastLoopbackMessages(x.Result, callback), TaskContinuationOptions.OnlyOnRanToCompletion | TaskContinuationOptions.RunContinuationsAsynchronously);

                    await task.ConfigureAwait(false);
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
            });
        }

        void FilterMulticastLoopbackMessages(UdpReceiveResult result, Action<UdpReceiveResult> next)
        {
            if (addresses.IndexOf(result.RemoteEndPoint.Address) <= 0)
            {
                next?.Invoke(result);
            }
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

        ~MulticastUdpListener()
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
