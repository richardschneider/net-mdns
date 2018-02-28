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
            TYPE = 6;
            TTL = TimeSpan.FromSeconds(0);
        }

        /// <summary>
        ///  The domain-name of the name server that was the
        //   original or primary source of data for this zone.
        /// </summary>
        public string MNAME { get; set; }

        /// <summary>
        ///  A domain-name which specifies the mailbox of the
        ///  person responsible for this zone.
        /// </summary>
        public string RNAME { get; set; }

        /// <summary>
        ///  The unsigned 32 bit version number of the original copy
        ///  of the zone.
        /// </summary>
        /// <remarks>
        ///  Zone transfers preserve this value. This
        ///  value wraps and should be compared using sequence space
        ///  arithmetic.
        /// </remarks>
        public uint SERIAL { get; set; }

        /// <summary>
        ///   Interval before the zone should be refreshed.
        /// </summary>
        public TimeSpan REFRESH { get; set; }

        /// <summary>
        ///   interval that should elapse before a failed refresh should be retried.
        /// </summary>
        public TimeSpan RETRY { get; set; }

        /// <summary>
        ///   Specifies the upper limit on
        ///   the time interval that can elapse before the zone is no
        ///   longer authoritative.
        /// </summary>
        public TimeSpan EXPIRE { get; set; }

        /// <summary>
        ///  Minimum TTL field that should be exported with any RR from this zone.
        /// </summary>
        public TimeSpan MINIMUM { get; set; }

        /// <inheritdoc />
        protected override void ReadData(DnsReader reader, int length)
        {
            MNAME = reader.ReadDomainName();
            RNAME = reader.ReadDomainName();
            SERIAL = reader.ReadUInt32();
            REFRESH = reader.ReadTimeSpan();
            RETRY = reader.ReadTimeSpan();
            EXPIRE = reader.ReadTimeSpan();
            MINIMUM = reader.ReadTimeSpan();
        }

        /// <inheritdoc />
        protected override void WriteData(DnsWriter writer)
        {
            writer.WriteDomainName(MNAME);
            writer.WriteDomainName(RNAME);
            writer.WriteUInt32(SERIAL);
            writer.WriteTimeSpan(REFRESH);
            writer.WriteTimeSpan(RETRY);
            writer.WriteTimeSpan(EXPIRE);
            writer.WriteTimeSpan(MINIMUM);
        }
    }
}
