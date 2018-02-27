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
        /// <param name="reader"></param>
        void Read(DnsReader reader);

        /// <summary>
        ///   Writes the DNS object.
        /// </summary>
        /// <param name="writer"></param>
        void Write(DnsWriter writer);
    }
}
