using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace Makaretu.Dns
{
    /// <summary>
    ///   Contains the IPv6 address of the named resource.
    /// </summary>
    public class AAAARecord : ResourceRecord
    {
        /// <summary>
        ///   Creates a new instance of the <see cref="AAAARecord"/> class.
        /// </summary>
        public AAAARecord() : base()
        {
            Type = 28;
            TTL = ResourceRecord.DefaultHostTTL;
        }

        /// <summary>
        ///    An IPv6 adresss.
        /// </summary>
        public IPAddress ADDRESS { get; set; }


        /// <inheritdoc />
        protected override void ReadData(DnsReader reader, int length)
        {
            ADDRESS = reader.ReadIPAddress(length);
        }

        /// <inheritdoc />
        protected override void WriteData(DnsWriter writer)
        {
            writer.WriteIPAddress(ADDRESS);
        }
    }
}
