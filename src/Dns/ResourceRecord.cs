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
        /// <value>
        ///   Defaults to <see cref="CLASS.IN"/>.
        /// </value>
        public CLASS CLASS { get; set; } = CLASS.IN;

        /// <summary>
        ///    Specifies the time interval
        ///    that the resource record may be cached before the source
        ///    of the information should again be consulted. 
        /// </summary>
        /// <value>
        ///    Defaults to 1 day.
        /// </value>
        /// <remarks>
        ///    Zero values are interpreted to mean that the RR can only be
        ///    used for the transaction in progress, and should not be
        ///    cached.
        ///    
        ///    For example, SOA records are always distributed
        ///    with a zero TTL to prohibit caching.Zero values can
        ///    also be used for extremely volatile data.
        /// </remarks>>
        public TimeSpan TTL { get; set; } = TimeSpan.FromDays(1);

        /// <inheritdoc />
        public override IDnsSerialiser Read(DnsReader reader)
        {
            // Read standard properties of a resource record.
            NAME = reader.ReadDomainName();
            TYPE = reader.ReadUInt16();
            CLASS = (CLASS)reader.ReadUInt16();
            TTL = reader.ReadTimeSpan();
            int length = reader.ReadUInt16();

            // Find a specific class for the TYPE or default
            // to UnknownRecord.
            ResourceRecord specific = new UnknownRecord();
            if (TYPE == 1) specific = new ARecord();
            if (TYPE == 28) specific = new AAAARecord();
            if (TYPE == 47) specific = new NSECRecord();
            specific.NAME = NAME;
            specific.TYPE = TYPE;
            specific.CLASS = CLASS;
            specific.TTL = TTL;
            specific.ReadData(reader, length);

            return specific;
        }

        /// <summary>
        ///   Read the data that is specific to the resource record <see cref="Type"/>.
        /// </summary>
        /// <param name="reader">
        ///   The source of the DNS object's data.
        /// </param>
        /// <param name="length">
        ///   The length, in bytes, of the data.
        /// </param>
        /// <remarks>
        ///   Derived classes must implement this method.
        /// </remarks>
        protected virtual void ReadData(DnsReader reader, int length)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public override void Write(DnsWriter writer)
        {
            writer.WriteDomainName(NAME);
            writer.WriteUInt16(TYPE);
            writer.WriteUInt16((ushort)CLASS);
            writer.WriteTimeSpan(TTL);

            writer.PushLengthPrefixedScope();
            WriteData(writer);
            writer.PopLengthPrefixedScope();
        }

        /// <summary>
        ///   Write the data that is specific to the resource record <see cref="Type"/>.
        /// </summary>
        /// <param name="writer">
        ///   The destination for the DNS object's data.
        /// </param>
        /// <remarks>
        ///   Derived classes must implement this method.
        /// </remarks>
        protected virtual void WriteData(DnsWriter writer)
        {
            throw new NotImplementedException();
        }
    }
}
