using System;
using System.Collections.Generic;
using System.Text;

namespace Makaretu.Dns
{
    /// <summary>
    ///   The type of query.
    /// </summary>
    /// <seealso cref="Message.Opcode"/>
    public enum MessageOperation : byte
    {
        /// <summary>
        ///   Standard query.
        /// </summary>
        Query = 0,

        /// <summary>
        ///   Inverse query.
        /// </summary>
        InverseQuery = 1,

        /// <summary>
        ///   A server status request.
        /// </summary>
        Status = 2
    }
}
