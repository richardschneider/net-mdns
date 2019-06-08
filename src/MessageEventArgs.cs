using System;
using System.Net;

namespace Makaretu.Dns
{
    /// <summary>
    ///   The event data for <see cref="MulticastService.QueryReceived"/> or
    ///   <see cref="MulticastService.AnswerReceived"/>.
    /// </summary>
    public class MessageEventArgs : EventArgs
    {
        /// <summary>
        ///   The DNS message.
        /// </summary>
        /// <value>
        ///   The received message.
        /// </value>
        public Message Message { get; set; }

        /// <summary>
        ///   The DNS message sender endpoint.
        /// </summary>
        /// <value>
        ///   The endpoint from the message was received.
        /// </value>
        public IPEndPoint RemoteEndPoint { get; set; }

        /// <summary>
        ///   Determines if the sender is using legacy unicast DNS.
        /// </summary>
        /// <value>
        ///   <b>false</b> if the sender is using port 5353.
        /// </value>
        public bool IsLegacyUnicast => RemoteEndPoint.Port != MulticastClient.MulticastPort;
    }
}

