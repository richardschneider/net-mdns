using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace Makaretu.Dns
{
    /// <summary>
    ///   An authoritative name server.
    /// </summary>
    /// <remarks>
    ///   NS records cause both the usual additional section processing to locate
    ///   a type A record, and, when used in a referral, a special search of the
    ///   zone in which they reside for glue information.
    ///   
    ///   The NS RR states that the named host should be expected to have a zone
    ///   starting at owner name of the specified class.  Note that the class may
    ///   not indicate the protocol family which should be used to communicate
    ///   with the host, although it is typically a strong hint.For example,
    ///   hosts which are name servers for either Internet (IN) or Hesiod (HS)
    ///   class information are normally queried using IN class protocols.
    /// </remarks>
    public class NSRecord : ResourceRecord
    {
        /// <summary>
        ///   Creates a new instance of the <see cref="NSRecord"/> class.
        /// </summary>
        public NSRecord() : base()
        {
            Type = 2;
        }

        /// <summary>
        ///   A domain-name which specifies a host which should be
        ///   authoritative for the specified class and domain.
        /// </summary>
        public string Authority { get; set; }


        /// <inheritdoc />
        protected override void ReadData(DnsReader reader, int length)
        {
            Authority = reader.ReadDomainName();
        }

        /// <inheritdoc />
        protected override void WriteData(DnsWriter writer)
        {
            writer.WriteDomainName(Authority);
        }
    }
}
