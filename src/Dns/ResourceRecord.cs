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
        public string Name { get; set; }

        /// <summary>
        ///    One of the RR TYPE codes.
        /// </summary>
        public ushort Type { get; set; }

        /// <summary>
        ///    One of the RR CLASS codes.
        /// </summary>
        /// <value>
        ///   Defaults to <see cref="Class.IN"/>.
        /// </value>
        public Class Class { get; set; } = Class.IN;

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
            Name = reader.ReadDomainName();
            Type = reader.ReadUInt16();
            Class = (Class)reader.ReadUInt16();
            TTL = reader.ReadTimeSpan();
            int length = reader.ReadUInt16();

            // Find a specific class for the TYPE or default
            // to UnknownRecord.
            ResourceRecord specific;
            if (ResourceRegistry.Records.TryGetValue(Type, out Func<ResourceRecord> maker))
            {
                specific = maker();
            }
            else
            {
                specific = new UnknownRecord();
            }
            specific.Name = Name;
            specific.Type = Type;
            specific.Class = Class;
            specific.TTL = TTL;

            // Read the specific properties of the resource record.
            specific.ReadData(reader, length);

            return specific;
        }

        /// <summary>
        ///   Read the data that is specific to the resource record <see cref="System.Type"/>.
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
            writer.WriteDomainName(Name);
            writer.WriteUInt16(Type);
            writer.WriteUInt16((ushort)Class);
            writer.WriteTimeSpan(TTL);

            writer.PushLengthPrefixedScope();
            WriteData(writer);
            writer.PopLengthPrefixedScope();
        }

        /// <summary>
        ///   Write the data that is specific to the resource record <see cref="System.Type"/>.
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
