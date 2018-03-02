using Makaretu.Dns;
using System;
using System.Collections.Generic;
using System.Text;

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
    }
}

