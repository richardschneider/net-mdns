using System;
using System.Collections.Generic;
using System.Text;

namespace Makaretu.Dns
{
    /// <summary>
    ///   Response codes.
    /// </summary>
    public enum MessageStatus : byte
    {
        /// <summary>
        ///    No error condition
        /// </summary>
        NoError = 0,

        /// <summary>
        ///    The name server was unable to interpret the query.
        /// </summary>
        FormatError = 1,

        /// <summary>
        ///    The name server was unable to process this query due to a
        ///    problem with the name server.
        /// </summary>
        ServerFailure = 2,

        /// <summary>
        ///    Meaningful only for responses from an authoritative name
        ///    server, this code signifies that the domain name 
        ///    referenced in the query does not exist.
        /// </summary>
        NameError = 3,

        /// <summary>
        ///    The name server does not support the requested kind of query.
        /// </summary>
        NotImplemented = 4,

        /// <summary>
        ///    The name server refuses to perform the specified operation for
        ///    policy reasons. 
        /// </summary>
        Refused = 5,
    }

}
