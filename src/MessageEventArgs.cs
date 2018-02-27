using Makaretu.Dns;
using System;
using System.Collections.Generic;
using System.Text;

namespace Makaretu.Mdns
{
    /// <summary>
    ///   The event data for <see cref="MdnsService.QueryReceived"/> or
    ///   <see cref="MdnsService.AnswerReceived"/>.
    /// </summary>
    public class MessageEventArgs : EventArgs
    {
        /// <summary>
        ///   The DNS message.
        /// </summary>
        public Message Message { get; set; }
    }
}

