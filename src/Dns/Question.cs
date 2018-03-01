using System;
using System.Collections.Generic;
using System.Text;

namespace Makaretu.Dns
{
    /// <summary>
    ///   A question about a domain name to resolve.
    /// </summary>
    public class Question : DnsObject
    {
        /// <summary>
        ///    A domain name to query.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        ///    A two octet code which specifies the type of the query.
        /// </summary>
        /// <remarks>
        ///    The values for this field include all codes valid for a
        ///    TYPE field, together with some more general codes which
        ///    can match more than one type of the resource record.
        /// </remarks>
        public ushort Type { get; set; }

        /// <summary>
        ///   A two octet code that specifies the class of the query.
        /// </summary>
        /// <value>
        ///   Defaults to <see cref="Class.IN"/>.
        /// </value>
        public Class Class { get; set; } = Class.IN;

        /// <inheritdoc />
        public override IDnsSerialiser Read(DnsReader reader)
        {
            Name = reader.ReadDomainName();
            Type = reader.ReadUInt16();
            Class = (Class)reader.ReadUInt16();

            return this;
        }

        /// <inheritdoc />
        public override void Write(DnsWriter writer)
        {
            writer.WriteDomainName(Name);
            writer.WriteUInt16(Type);
            writer.WriteUInt16((ushort)Class);
        }
    }
}
