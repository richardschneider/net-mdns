using System;
using System.Collections.Generic;
using System.Text;

namespace Makaretu.Dns
{
    /// <summary>
    ///   Defines the serialisation of DNS objects.
    /// </summary>
    public interface IDnsSerialiser
    {
        /// <summary>
        ///   Reads the DNS object.
        /// </summary>
        /// <param name="reader">
        ///   The source of the DNS object.
        /// </param>
        /// <returns>
        ///   The final DNS object.
        /// </returns>
        /// <remarks>
        ///   Reading a <see cref="ResourceRecord"/> will return a new instance that
        ///   is type specific.
        /// </remarks>
        IDnsSerialiser Read(DnsReader reader);

        /// <summary>
        ///   Writes the DNS object.
        /// </summary>
        /// <param name="writer">
        ///   The destination of the DNS object.
        /// </param>
        void Write(DnsWriter writer);
    }
}
