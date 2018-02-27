using System;
using System.Collections.Generic;
using System.Text;

namespace Makaretu.Dns
{
    /// <summary>
    ///   Contains some information on the named resource.
    /// </summary>
    public class ResourceRecord : DnsObject
    {
        /// <summary>
        ///   An owner name, i.e., the name of the node to which this
        ///   resource record pertains
        /// </summary>
        public string NAME { get; set; }

        /// <summary>
        ///    One of the RR TYPE codes.
        /// </summary>
        public ushort TYPE { get; set; }

        /// <summary>
        ///    One of the RR CLASS codes.
        /// </summary>
        public ushort CLASS { get; set; }

        /// <summary>
        ///    Specifies the time interval
        ///    that the resource record may be cached before the source
        ///    of the information should again be consulted. 
        ///    
        ///    Zero values are interpreted to mean that the RR can only be
        ///    used for the transaction in progress, and should not be
        ///    cached.
        ///    
        ///    For example, SOA records are always distributed
        ///    with a zero TTL to prohibit caching.Zero values can
        ///    also be used for extremely volatile data.
        /// </summary>
        public TimeSpan TTL { get; set; }

        /// <inheritdoc />
        public override IDnsSerialiser Read(DnsReader reader)
        {
            NAME = reader.ReadDomainName();
            TYPE = reader.ReadUInt16();
            CLASS = reader.ReadUInt16();
            TTL = reader.ReadTimeSpan();
            int length = reader.ReadUInt16();
            var data = reader.ReadBytes(length);

            // TODO: ReadData(length, reader)

            return this;
        }

        /// <inheritdoc />
        public override void Write(DnsWriter writer)
        {
            writer.WriteString(NAME);
            writer.WriteUInt16(TYPE);
            writer.WriteUInt16(CLASS);
            writer.WriteTimeSpan(TTL);
            // TODO: WriteData(writer)
        }
    }
}
