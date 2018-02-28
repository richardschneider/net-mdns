using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace Makaretu.Dns
{
    /// <summary>
    ///   Mail exchange.
    /// </summary>
    /// <remarks>
    ///   MX records cause type A additional section processing for the host
    ///   specified by EXCHANGE.The use of MX RRs is explained in detail in
    ///   [RFC-974].
    /// </remarks>
    public class MXRecord : ResourceRecord
    {
        /// <summary>
        ///   Creates a new instance of the <see cref="MXRecord"/> class.
        /// </summary>
        public MXRecord() : base()
        {
            TYPE = 15;
        }

        /// <summary>
        ///  A 16 bit integer which specifies the preference given to
        ///  this RR among others at the same owner.Lower values
        ///  are preferred.
        /// </summary>
        public ushort PREFERENCE { get; set; }

        /// <summary>
        ///  A domain-name which specifies a host willing to act as
        ///  a mail exchange for the owner name.
        /// </summary>
        public string EXCHANGE { get; set; }


        /// <inheritdoc />
        protected override void ReadData(DnsReader reader, int length)
        {
            PREFERENCE = reader.ReadUInt16();
            EXCHANGE = reader.ReadDomainName();
        }

        /// <inheritdoc />
        protected override void WriteData(DnsWriter writer)
        {
            writer.WriteUInt16(PREFERENCE);
            writer.WriteDomainName(EXCHANGE);
        }
    }
}
