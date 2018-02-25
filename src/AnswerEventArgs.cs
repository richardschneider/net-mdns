using System;
using System.Collections.Generic;
using System.Text;

namespace Makaretu.Mdns
{
    /// <summary>
    ///   The event data for <see cref="MdnsService.AnswerReceived"/>.
    /// </summary>
    public class AnswerEventArgs : EventArgs
    {
        /// <summary>
        ///   The address of the peer that was discovered.
        /// </summary>
        /// <value>
        ///   The address must end with the ipfs protocol and the public ID
        ///   of the peer.  For example "/ip4/104.131.131.82/tcp/4001/ipfs/QmaCpDMGvV2BGHeYERUEnRQAwe3N8SzbUtfsmvsqQLuvuJ"
        /// </value>
        public object Answer { get; set; }
    }
}

