using Common.Logging;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Text;

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
        static readonly IPNetwork[] linkLocalNetworks = new IPNetwork[]
        {
            IPNetwork.Parse("169.254.0.0/16"),
            IPNetwork.Parse("fe80::/10")
        };

        const int MulticastPort = 5353;
        // IP header (20 bytes for IPv4; 40 bytes for IPv6) and the UDP header(8 bytes).
        const int packetOverhead = 48;
        const int maxDatagramSize = Message.MaxLength;

        CancellationTokenSource serviceCancellation;
        CancellationTokenSource listenerCancellation;

        List<NetworkInterface> knownNics = new List<NetworkInterface>();
        readonly bool ip6;
        IPEndPoint mdnsEndpoint;
        int maxPacketSize;

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
        ConcurrentDictionary<string, DateTime> sentMessages = new ConcurrentDictionary<string, DateTime>();

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
        readonly Object senderLock = new object();

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
        public static IEnumerable<NetworkInterface> GetNetworkInterfaces()
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
            serviceCancellation = new CancellationTokenSource();
            maxPacketSize = maxDatagramSize - packetOverhead;
            knownNics.Clear();

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
            var currentNics = GetNetworkInterfaces().ToList();
            var newNics = new List<NetworkInterface>();
            var oldNics = new List<NetworkInterface>();

            lock (senderLock)
            {
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
            }
            knownNics = currentNics;

            // If any NIC change, then get new sockets.
            if (newNics.Any() || oldNics.Any())
            {
                // Recreate the sender
                if (sender != null)
                {
                    sender.Dispose();
                }
                sender = new UdpClient(mdnsEndpoint.AddressFamily);
                sender.JoinMulticastGroup(mdnsEndpoint.Address);
                sender.MulticastLoopback = true;

                // Start a task to listen for MDNS messages.
                Listener();
            }

            // Tell others.
            if (newNics.Any())
            { 
                NetworkInterfaceDiscovered?.Invoke(this, new NetworkInterfaceEventArgs
                {
                    NetworkInterfaces = newNics
                });
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
                tsc.SetCanceled();
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
        ///   <para>
        ///   <paramref name="checkDuplicate"/> should always be <b>true</b> except
        ///   when <see href="https://tools.ietf.org/html/rfc6762#section-8.1">answering a probe</see>.
        ///   </para>
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

        void Send(Message msg, bool checkDuplicate)
        {
            var packet = msg.ToByteArray();
            if (packet.Length > maxPacketSize)
            {
                throw new ArgumentOutOfRangeException($"Exceeds max packet size of {maxPacketSize}.");
            }

            // Get the hash of the packet.  MD5 is okay because
            // the hash is not used for security.
            string hash;
            using (var md5 = MD5.Create())
            {
                var bytes = md5.ComputeHash(packet);
                // TODO: there must be a more efficient way.
                var s = new StringBuilder();
                for (int i = 0; i < bytes.Length; i++)
                {
                    s.Append(bytes[i].ToString("x2"));
                }
                hash = s.ToString();
            }

            // Prune the sent messages.  Anything older than a second ago
            // is removed.
            var now = DateTime.Now;
            var dead = now.AddSeconds(-1);
            foreach (var notrecent in sentMessages.Where(x => x.Value < dead))
            {
                sentMessages.TryRemove(notrecent.Key, out DateTime _);
            }

            // If messsage was recently sent, then do not send again.
            if (checkDuplicate && sentMessages.ContainsKey(hash))
            {
                return;
            }

            lock (senderLock)
            {
                if (sender == null)
                    throw new InvalidOperationException("MDNS is not started");
                sender.SendAsync(packet, packet.Length, mdnsEndpoint).Wait();
            }

            sentMessages.AddOrUpdate(hash, DateTime.Now, (key, value) => value);
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
                if (msg.IsQuery && msg.Questions.Count > 0)
                {
                    QueryReceived?.Invoke(this, new MessageEventArgs { Message = msg });
                }
                else if (msg.IsResponse && msg.Answers.Count > 0)
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
            var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

            // Stop the previous listener.
            if (listenerCancellation != null)
            {
                listenerCancellation.Cancel();
            }

            listenerCancellation = new CancellationTokenSource();
            UdpClient receiver = new UdpClient(mdnsEndpoint.AddressFamily);
            if (isWindows)
            {
                receiver.ExclusiveAddressUse = false;
            }
            receiver.Client.SetSocketOption(
                SocketOptionLevel.Socket, 
                SocketOptionName.ReuseAddress,
                true);
            if (isWindows)
            {
                receiver.ExclusiveAddressUse = false;
            }
            var endpoint = new IPEndPoint(ip6 ? IPAddress.IPv6Any : IPAddress.Any, MulticastPort);
            receiver.Client.Bind(endpoint);
            receiver.JoinMulticastGroup(mdnsEndpoint.Address);

            var cancel = listenerCancellation.Token;
            cancel.Register(() => receiver.Dispose());
            try
            {
                while (!cancel.IsCancellationRequested)
                {
                    var result = await receiver.ReceiveAsync();
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
            if (listenerCancellation != null)
            {
                listenerCancellation.Dispose();
                listenerCancellation = null;
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
