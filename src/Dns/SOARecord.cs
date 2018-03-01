using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace Makaretu.Dns
{
    /// <summary>
    ///   Marks the start of a zone of authority.
    /// </summary>
    /// <remarks>
    ///   Most of these fields are pertinent only for name server maintenance
    ///   operations.  However, MINIMUM is used in all query operations that
    ///   retrieve RRs from a zone.Whenever a RR is sent in a response to a
    ///   query, the TTL field is set to the maximum of the TTL field from the RR
    ///   and the MINIMUM field in the appropriate SOA.Thus MINIMUM is a lower
    ///   bound on the TTL field for all RRs in a zone.Note that this use of
    ///   MINIMUM should occur when the RRs are copied into the response and not
    ///   when the zone is loaded from a master file or via a zone transfer.  The
    ///   reason for this provison is to allow future dynamic update facilities to
    ///   change the SOA RR with known semantics.
    /// </remarks>
    public class SOARecord : ResourceRecord
    {
        /// <summary>
        ///   Creates a new instance of the <see cref="SOARecord"/> class.
        /// </summary>
        /// <remarks>
        ///   Sets <see cref="ResourceRecord.TTL"/> to zero.
        /// </remarks>
        public SOARecord() : base()
        {
            Type = 6;
            TTL = TimeSpan.FromSeconds(0);
        }

        /// <summary>
        ///  The domain-name of the name server that was the
        ///  original or primary source of data for this zone.
        /// </summary>
        public string PrimaryName { get; set; }

        /// <summary>
        ///  A domain-name which specifies the mailbox of the
        ///  person responsible for this zone.
        /// </summary>
        public string Mailbox { get; set; }

        /// <summary>
        ///  The unsigned 32 bit version number of the original copy
        ///  of the zone.
        /// </summary>
        /// <remarks>
        ///  Zone transfers preserve this value. This
        ///  value wraps and should be compared using sequence space
        ///  arithmetic.
        /// </remarks>
        public uint SerialNumber { get; set; }

        /// <summary>
        ///   Interval before the zone should be refreshed.
        /// </summary>
        public TimeSpan Refresh { get; set; }

        /// <summary>
        ///   interval that should elapse before a failed refresh should be retried.
        /// </summary>
        public TimeSpan Retry { get; set; }

        /// <summary>
        ///   Specifies the upper limit on
        ///   the time interval that can elapse before the zone is no
        ///   longer authoritative.
        /// </summary>
        public TimeSpan Expire { get; set; }

        /// <summary>
        ///  Minimum TTL field that should be exported with any RR from this zone.
        /// </summary>
        public TimeSpan Minimum { get; set; }

        /// <inheritdoc />
        protected override void ReadData(DnsReader reader, int length)
        {
            PrimaryName = reader.ReadDomainName();
            Mailbox = reader.ReadDomainName();
            SerialNumber = reader.ReadUInt32();
            Refresh = reader.ReadTimeSpan();
            Retry = reader.ReadTimeSpan();
            Expire = reader.ReadTimeSpan();
            Minimum = reader.ReadTimeSpan();
        }

        /// <inheritdoc />
        protected override void WriteData(DnsWriter writer)
        {
            writer.WriteDomainName(PrimaryName);
            writer.WriteDomainName(Mailbox);
            writer.WriteUInt32(SerialNumber);
            writer.WriteTimeSpan(Refresh);
            writer.WriteTimeSpan(Retry);
            writer.WriteTimeSpan(Expire);
            writer.WriteTimeSpan(Minimum);
        }
    }
}
