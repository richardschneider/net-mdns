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
        ///    A domain name.
        /// </summary>
        public string QNAME { get; set; }

        /// <summary>
        ///    A two octet code which specifies the type of the query.
        ///    The values for this field include all codes valid for a
        ///    TYPE field, together with some more general codes which
        ///    can match more than one type of the resource record.
        /// </summary>
        public ushort QTYPE { get; set; }

        /// <summary>
        ///   A two octet code that specifies the class of the query.
        /// </summary>
        public CLASS QCLASS { get; set; }

        /// <inheritdoc />
        public override void Read(DnsReader reader)
        {
            QNAME = reader.ReadDomainName();
            QTYPE = reader.ReadUInt16();
            QCLASS = (CLASS)reader.ReadUInt16();
        }

        /// <inheritdoc />
        public override void Write(DnsWriter writer)
        {
            writer.WriteDomainName(QNAME);
            writer.WriteUInt16(QTYPE);
            writer.WriteUInt16((ushort)QCLASS);
        }
    }
}
