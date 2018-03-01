using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace Makaretu.Dns
{
    /// <summary>
    ///   Contains the IPv4 address of the named resource.
    /// </summary>
    public class ARecord : ResourceRecord
    {
        /// <summary>
        ///   Creates a new instance of the <see cref="ARecord"/> class.
        /// </summary>
        public ARecord() : base()
        {
            Type = 1;
        }

        /// <summary>
        ///    A 32 bit Internet address (IPv4).
        /// </summary>
        public IPAddress Address { get; set; }


        /// <inheritdoc />
        protected override void ReadData(DnsReader reader, int length)
        {
            Address = reader.ReadIPAddress(length);
        }

        /// <inheritdoc />
        protected override void WriteData(DnsWriter writer)
        {
            writer.WriteIPAddress(Address);
        }
    }
}
